using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

namespace Jobs
{
    [BurstCompile]
    public struct HealingJob: IJobParallelFor
    {
        public NativeArray<int> states;
        public NativeArray<float> infectionTimers;

        public float deltaTime;

        public void Execute(int i)
        {
            if(states[i] != 1) return;

            infectionTimers[i] -= deltaTime;

            if(infectionTimers[i] <= 0)
                states[i] = 2;
        }
    }
}