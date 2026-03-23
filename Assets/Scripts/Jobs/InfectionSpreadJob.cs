using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst; 

namespace Jobs
{
    [BurstCompile]
    public struct InfectionSpreadJob: IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> positions;
        [NativeDisableParallelForRestriction]
        public NativeArray<int> states;
        [NativeDisableParallelForRestriction]
        public NativeArray<float> infectionTimers;

        [ReadOnly] public NativeParallelMultiHashMap<int,int> grid;
        [ReadOnly] public NativeArray<bool> masked;

        public float infectionRadius;
        public float infectionDuration;
        public float infectionProbabilityFrame;
        public float cellSize;
        public float maskReduction;
        
        const int CELL_OFFSET = 60;

        public void Execute(int i)
        {
            if(states[i] != 1) return;

            float2 pos = positions[i];

            int2 cell = (int2)math.floor(pos / cellSize);

            for(int x=-1;x<=1;x++)
            for(int y=-1;y<=1;y++)
            {
                int2 neighbor = cell + new int2(x,y);

                int hash = neighbor.x * 73856093 ^ neighbor.y * 19349663;

                NativeParallelMultiHashMapIterator<int> it;
                int other;

                if(grid.TryGetFirstValue(hash,out other,out it))
                {
                    do
                    {
                        if(states[other] == 0)
                        {
                            float d = math.distance(pos,positions[other]);

                            if(d < infectionRadius)
                            {
                                float p = infectionProbabilityFrame;
                                
                                if(masked[i]) 
                                    p *= (1f - maskReduction);
                                if(masked[other]) 
                                    p *= (1f - maskReduction);
                                
                                uint seed = (uint)(i * 928371 + other);
                                var rng = new Unity.Mathematics.Random(seed);

                                if(rng.NextFloat() <= p)
                                {
                                    states[other] = 1;
                                    infectionTimers[other] = infectionDuration;
                                }
                            }
                        }

                    } while(grid.TryGetNextValue(out other,ref it));
                }
            }
        }
    }
}