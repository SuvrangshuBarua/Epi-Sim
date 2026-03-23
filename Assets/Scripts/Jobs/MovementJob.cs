using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

namespace Jobs
{
    [BurstCompile]
    public struct MovementJob : IJobParallelFor
    {
        public NativeArray<float2> positions;
        public NativeArray<float2> directions;

        public float speed;
        public float deltaTime;
        public float boundaryMin; 
        public float boundaryMax;  

        public void Execute(int i)
        {
            float2 pos = positions[i];
            float2 dir = directions[i];

            pos += dir * speed * deltaTime;
            
            if (pos.x < boundaryMin) { pos.x = boundaryMin; dir.x = math.abs(dir.x);  }
            if (pos.x > boundaryMax) { pos.x = boundaryMax; dir.x = -math.abs(dir.x); }
            if (pos.y < boundaryMin) { pos.y = boundaryMin; dir.y = math.abs(dir.y);  }
            if (pos.y > boundaryMax) { pos.y = boundaryMax; dir.y = -math.abs(dir.y); }

            positions[i]  = pos;
            directions[i] = dir;
        }
    }
}