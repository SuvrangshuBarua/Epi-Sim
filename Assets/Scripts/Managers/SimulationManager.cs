using Jobs;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;  
using System.Collections.Generic;

public enum AgentState
{
    Susceptible = 0, 
    Infected, 
    Removed
}

public class SimulationManager : MonoBehaviour
{
    public int agentCount = 20000;

    public float speed = 2f;
    public float infectionRadius = 0.5f;
    public float infectionDuration = 5f;
    public float infectionProbabilityPerSecond = 0.4f;
    
    public uint randomSeed = 1234;
    public int renderLayer = 7;
    
    [Header("Masks")] [Range(0f, 1f)] 
    public float maskAdoptionRate = 0f;
    [Range(0f, 1f)] 
    public float maskReduction = 0.7f;
    
    [Header("SIR Graph")]
    public string graphTitle = "Simulation";
    public int    maxDataPoints  = 500;
    public Rect   graphRect      = new Rect(10, 10, 400, 200);

    internal  List<float> sHistory = new List<float>();
    internal List<float> iHistory = new List<float>();
    internal List<float> rHistory = new List<float>();

    private Texture2D graphBackground;
    private Texture2D sLine;
    private Texture2D iLine;
    private Texture2D rLine;
    
    public float boundaryMin = -5f;
    public float boundaryMax = 5f;

    public Mesh agentMesh;
    public Material agentMaterial;

    private NativeArray<float2> positions;
    private NativeArray<float2> directions;
    private NativeArray<int> states;
    private NativeArray<float> infectionTimers;
    private NativeArray<bool> masked;

    private NativeParallelMultiHashMap<int,int> spatialGrid;

    private ComputeBuffer matricesBuffer;
    private ComputeBuffer statesBuffer;
    private ComputeBuffer argsBuffer;

    private Matrix4x4[] matrices;
    private int[] statesArray;
    
    private Bounds renderBounds;


    private float cellSize;
    private Material instanceMaterial;

    private void Start()
    {
        cellSize = infectionRadius;
        instanceMaterial = new Material(agentMaterial);

        positions = new NativeArray<float2>(agentCount, Allocator.Persistent);
        directions = new NativeArray<float2>(agentCount, Allocator.Persistent);
        states = new NativeArray<int>(agentCount, Allocator.Persistent);
        infectionTimers = new NativeArray<float>(agentCount, Allocator.Persistent);
        masked = new NativeArray<bool>(agentCount, Allocator.Persistent);

        spatialGrid = new NativeParallelMultiHashMap<int,int>(agentCount, Allocator.Persistent);

        matrices = new Matrix4x4[agentCount];
        statesArray   = new int[agentCount];

        matricesBuffer = new ComputeBuffer(agentCount, sizeof(float) * 16);
        statesBuffer   = new ComputeBuffer(agentCount, sizeof(int));
        
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        args[0] = (uint)agentMesh.GetIndexCount(0);
        args[1] = (uint)agentCount;
        args[2] = (uint)agentMesh.GetIndexStart(0);
        args[3] = (uint)agentMesh.GetBaseVertex(0);
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint),
            ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);

        renderBounds = new Bounds(Vector3.zero, Vector3.one * 100f);

        Unity.Mathematics.Random rng = new Unity.Mathematics.Random(randomSeed);

        for(int i=0;i<agentCount;i++)
        {
            positions[i] = rng.NextFloat2(boundaryMin, boundaryMax);
            directions[i] = math.normalize(rng.NextFloat2(-1f,1f));
            states[i] = (int)AgentState.Susceptible;
            infectionTimers[i] = 0;
            masked[i] = rng.NextFloat() < maskAdoptionRate;
        }

        states[0] = (int)AgentState.Infected;
        infectionTimers[0] = infectionDuration;
        GraphParamSetter();
    }
    
    private void Update()
    {
        float dt = Time.deltaTime;
        float pf = 1f - math.pow(1f - infectionProbabilityPerSecond, dt);

        spatialGrid.Clear();

        var moveJob = new Jobs.MovementJob
        {
            positions = positions,
            directions = directions,
            speed = speed,
            boundaryMin = boundaryMin,
            boundaryMax = boundaryMax,
            deltaTime = dt
        };

        var gridJob = new Jobs.BuildGridJob
        {
            positions = positions,
            grid = spatialGrid.AsParallelWriter(),
            cellSize = cellSize
        };

        var infectionJob = new Jobs.InfectionSpreadJob
        {
            positions = positions,
            states = states,
            infectionTimers = infectionTimers,
            grid = spatialGrid,
            masked = masked,
            infectionRadius = infectionRadius,
            infectionDuration = infectionDuration,
            infectionProbabilityFrame = pf,
            cellSize = cellSize,
            maskReduction = maskReduction
            
        };

        var healJob = new Jobs.HealingJob
        {
            states = states,
            infectionTimers = infectionTimers,
            deltaTime = dt
        };

        JobHandle moveHandle = moveJob.Schedule(agentCount,64);
        JobHandle gridHandle = gridJob.Schedule(agentCount,64,moveHandle);
        JobHandle spreadHandle = infectionJob.Schedule(agentCount,64,gridHandle);
        JobHandle healHandle = healJob.Schedule(agentCount,64,spreadHandle);
        healHandle.Complete();

        RenderAgents();
        
        int sCount = 0, iCount = 0, rCount = 0;

        for (int i = 0; i < agentCount; i++)
        {
            if      (states[i] == (int)AgentState.Susceptible) sCount++;
            else if (states[i] == (int)AgentState.Infected)    iCount++;
            else                                               rCount++;
        }

        float norm = 1f / agentCount;
        sHistory.Add(sCount * norm);
        iHistory.Add(iCount * norm);
        rHistory.Add(rCount * norm);
        
        if (sHistory.Count > maxDataPoints)
        {
            sHistory.RemoveAt(0);
            iHistory.RemoveAt(0);
            rHistory.RemoveAt(0);
        }

    }

    void RenderAgents()
    {
        for (int i = 0; i < agentCount; i++)
        {
            Vector3 pos    = new Vector3(positions[i].x, 0, positions[i].y);
            matrices[i]    = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * 0.2f);
            statesArray[i] = states[i];
        }
 
        matricesBuffer.SetData(matrices);
        statesBuffer.SetData(statesArray);
 
        instanceMaterial.SetBuffer("_Matrices", matricesBuffer);
        instanceMaterial.SetBuffer("_States",   statesBuffer);
 
        Graphics.DrawMeshInstancedIndirect(
            agentMesh, 0, instanceMaterial, renderBounds, argsBuffer,
            0, null, UnityEngine.Rendering.ShadowCastingMode.Off, false,
            renderLayer);
    }

    void OnDestroy()
    {
        if (positions.IsCreated)       positions.Dispose();
        if (directions.IsCreated)      directions.Dispose();
        if (states.IsCreated)          states.Dispose();
        if (infectionTimers.IsCreated) infectionTimers.Dispose();
        if (masked.IsCreated)          masked.Dispose();
        if (spatialGrid.IsCreated)     spatialGrid.Dispose();
        if (matricesBuffer != null)    matricesBuffer.Release();
        if (statesBuffer   != null)    statesBuffer.Release();
        if (argsBuffer     != null)    argsBuffer.Release();
        if (instanceMaterial != null) Destroy(instanceMaterial);
    }

    #region  Graph Representation
    private void GraphParamSetter()
    {
        graphBackground = CreateGraphTex(1, 1, new Color(0.05f, 0.05f, 0.05f, 0.85f));
        sLine           = CreateGraphTex(1, 1, new Color(0.2f,  0.9f,  0.2f,  1f));
        iLine           = CreateGraphTex(1, 1, new Color(0.9f,  0.1f,  0.1f,  1f));
        rLine           = CreateGraphTex(1, 1, new Color(0.6f,  0.6f,  0.6f,  1f));
    }
    private Texture2D CreateGraphTex(int w, int h, Color col)
    {
        var t = new Texture2D(w, h);
        t.SetPixel(0, 0, col);
        t.Apply();
        return t;
    }
    void OnGUI()
    {
        if (sHistory.Count < 2) return;

        float x = graphRect.x;
        float y = graphRect.y;
        float w = graphRect.width;
        float h = graphRect.height;
        
        GUI.DrawTexture(graphRect, graphBackground);
        
        GUI.color = Color.white;
        GUI.Label(new Rect(x + 10, y + 5, 200, 20), graphTitle, "BoldLabel");
        
        DrawGridLines(x, y, w, h);
        
        DrawCurve(sHistory, x, y, w, h, new Color(0.2f, 0.9f, 0.2f));
        DrawCurve(iHistory, x, y, w, h, new Color(0.9f, 0.1f, 0.1f));
        DrawCurve(rHistory, x, y, w, h, new Color(0.6f, 0.6f, 0.6f));
        
        float legendX = x + w - 80;
        float legendY = y + 10;

        DrawLegendItem(legendX, legendY,      new Color(0.2f, 0.9f, 0.2f), $"S {sHistory[sHistory.Count-1]*agentCount:0}");
        DrawLegendItem(legendX, legendY + 18, new Color(0.9f, 0.1f, 0.1f), $"I {iHistory[iHistory.Count-1]*agentCount:0}");
        DrawLegendItem(legendX, legendY + 36, new Color(0.6f, 0.6f, 0.6f), $"R {rHistory[rHistory.Count-1]*agentCount:0}");
        
        GUI.color = new Color(1,1,1,0.5f);
        GUI.Label(new Rect(x + 2, y + 5,          40, 15), "100%");
        GUI.Label(new Rect(x + 2, y + h/2 - 7,   40, 15), "50%");
        GUI.Label(new Rect(x + 2, y + h - 15,     40, 15), "0%");

        GUI.color = Color.white;
    }

    void DrawGridLines(float x, float y, float w, float h)
    {
        GUI.color = new Color(1, 1, 1, 0.08f);
        
        for (int i = 1; i < 4; i++)
        {
            float lineY = y + h * (i / 4f);
            GUI.DrawTexture(new Rect(x, lineY, w, 1), Texture2D.whiteTexture);
        }
        
        for (int i = 1; i < 4; i++)
        {
            float lineX = x + w * (i / 4f);
            GUI.DrawTexture(new Rect(lineX, y, 1, h), Texture2D.whiteTexture);
        }

        GUI.color = Color.white;
    }

    void DrawCurve(List<float> data, float x, float y, float w, float h, Color col)
    {
        if (data.Count < 2) return;
        
        GUI.color = col;
        int count = data.Count;

        for (int i = 1; i < count; i++)
        {
            float x0 = x + (i - 1) / (float)(maxDataPoints - 1) * w;
            float x1 = x + i       / (float)(maxDataPoints - 1) * w;
            float y0 = y + h - data[i - 1] * h;
            float y1 = y + h - data[i]     * h;
            DrawLine(x0, y0, x1, y1, 2f, col);
        }

        GUI.color = Color.white;
    }

    void DrawLine(float x0, float y0, float x1, float y1, float thickness, Color col)
    {
        float dx  = x1 - x0;
        float dy  = y1 - y0;
        float len = Mathf.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f) return;

        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
        Matrix4x4 matrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, new Vector2(x0, y0));
        GUI.color = col;
        
        GUI.DrawTexture(
            new Rect(x0, y0 - thickness * 0.5f, len, thickness),
            Texture2D.whiteTexture
        );
        GUI.matrix = matrix; 
    }

    void DrawLegendItem(float x, float y, Color col, string label)
    {
        GUI.color = col;
        GUI.DrawTexture(new Rect(x, y + 4, 12, 4), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(x + 16, y, 80, 18), label);
    }
    #endregion
}