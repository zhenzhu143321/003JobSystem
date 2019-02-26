using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

public struct IndentationJob : IJobParallelFor {

	public NativeArray<Vector3> contactPoints;//接触点
	public NativeArray<Vector3> initialVertices;//初始化点
	public NativeArray<Vector3> modifiedVertices;//修改后的点,即结果

	public float force;//力度
	public float radius;//半径

	public void Execute(int i)
	{
		for(int c = 0; c < contactPoints.Length; c++)
		{
			Vector3 pointToVert = (modifiedVertices[i] - contactPoints[c]);
			float distance = pointToVert.sqrMagnitude;

			if(distance < radius)
			{
				Vector3 newVertice = initialVertices[i] + Vector3.down * (force);
				modifiedVertices[i] = newVertice;
			}
			
		}
	}
}
