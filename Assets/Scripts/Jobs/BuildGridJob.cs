using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

namespace Jobs
{
    [BurstCompile]
    public struct BuildGridJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> positions;
        public NativeParallelMultiHashMap<int,int>.ParallelWriter grid;

        public float cellSize;
        const int CELL_OFFSET = 60;

        public void Execute(int i)
        {
            int2 cell = (int2)math.floor(positions[i] / cellSize);

            int hash = cell.x * 73856093 ^ cell.y * 19349663;

            grid.Add(hash, i);
        }
    }
}