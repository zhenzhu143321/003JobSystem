using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

[RequireComponent(typeof(MeshFilter),(typeof(MeshRenderer)))]
public class DeformableMesh : MonoBehaviour 
{
	[Header("Size Settings:")]
	[SerializeField] float verticalSize;//高
	[SerializeField] float horizontalSize;//宽

	[Header("Material:")]
	[SerializeField] Material meshMaterial;//材质

	[Header("Indentation Settings:")]
	[SerializeField] float force;//力度
	[SerializeField] float radius;//半径

	Mesh mesh;
	MeshFilter meshFilter;
	MeshRenderer meshRenderer;
	MeshCollider meshCollider;

	//MeshInformation
	Vector3[] vertices;
	Vector3[] modifiedVertices;//修改后点的，传出结果
	int[] triangles;//三角形

	Vector2 verticeAmount;//点数量
	
	List<HandledResult> scheduledJobsList = new List<HandledResult>();//工作句柄列表

	void Awake() 
	{
		meshRenderer = GetComponent<MeshRenderer>();
		meshFilter = GetComponent<MeshFilter>();
		meshFilter.mesh = new Mesh();
		mesh = meshFilter.mesh;

		GeneratePlane();//生成地面	

	}

	void Update() 
	{
		if(scheduledJobsList.Count > 0)
		{
			for(int i = 0; i < scheduledJobsList.Count; i++)
			{
				CompleteJob(scheduledJobsList[i]);
			}
		}
    }

	/*网格是由顶点和三角形构建的，基本上由其中的三个顶点构建。我们首先处理顶点的
     * 位置。顶点需要Vector3数组，因为它们在世界空间中拥有3D位置。数组的长度取决于
     * 所生成平面的大小。简单来说，可以想象平面顶部有网格覆盖，每个网格区域的每个角
     * 都需要一个顶点，相邻区域可以共享同一个角。因此，在每个维度中，顶点的数量需要
     * 比区域的数量多1。*/ 
	void GeneratePlane()
	{
		vertices = new Vector3[((int)horizontalSize + 1) * 
		((int)verticalSize + 1)];
		Vector2[] uv = new Vector2[vertices.Length];

        /*现在使用嵌套的for循环相应地定位顶点*/
        for (int z = 0, y = 0; y <= (int)verticalSize; y++)
		{
			for(int x = 0; x <= (int)horizontalSize; x++, z++)
			{
				vertices[z] = new Vector3(x,0,y);
				uv[z] = new Vector2(x/(int)horizontalSize,
				y/(int)verticalSize);
			}
		}

        /*我们已经生成并定位了顶点，应该开始生成合适的网格。首先设置这些顶点为
         * 网格顶点 */
        mesh.vertices = vertices;

        //我们还需要确保我们的顶点和修改的顶点在一开始就相互匹配 
        modifiedVertices = new Vector3[vertices.Length];
		for(int i = 0; i < vertices.Length; i++)
		{
			modifiedVertices[i] = vertices[i];
		}

		mesh.uv = uv;

        /*网格此时还不会出现，因为它没有任何三角形。我们会通过循环构成三角形的点来生
         *成三角形，这些三角形的标签会进入int类型的triangles数组中 */
        triangles = new int[(int)horizontalSize * 
		(int)verticalSize * 6];

		for(int t = 0, v = 0, y = 0; y < (int)verticalSize; y++, v++)
		{
			for(int x = 0; x <(int)horizontalSize; x++, t+= 6, v++)
			{
				triangles[t] = v;
				triangles[t + 3] = triangles[t + 2] = v + 1; 
				triangles[t + 4] = triangles[t + 1] = v + (int)horizontalSize + 1;
				triangles[t + 5] = v + (int)horizontalSize + 2;
			}
		}

        /*最后，我们需要将三角形指定为网格三角形，然后重新计算法线，确保得到正确的光照效果*/
        mesh.triangles = triangles;
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();
		mesh.RecalculateTangents();

        /*我们还需要碰撞体，从而能够使用物理系统检测交互*/
        meshCollider = gameObject.AddComponent<MeshCollider>();
		meshCollider.sharedMesh = mesh;
        
        //我们需要设置网格材质，以避免出现难看的红色平面
        meshRenderer.material = meshMaterial;
	}

	void OnCollisionEnter(Collision other) {
		if(other.contacts.Length > 0)
    	{
     		Vector3[] contactPoints = new Vector3[other.contacts.Length];//保存接触点
      		for(int i = 0; i < other.contacts.Length; i++)
      		{
        		Vector3 currentContactpoint = other.contacts[i].point;//当前接触点
				currentContactpoint = transform.InverseTransformPoint(currentContactpoint);
        		contactPoints[i] = currentContactpoint;
      		}

			HandledResult newHandledResult = new HandledResult();
			IndentSnow(force,contactPoints,ref newHandledResult);
     
    	}
	}

	public void AddForce(Vector3 inputPoint)
	{
		StartCoroutine(MarkHitpointDebug(inputPoint));

	}

	

	IEnumerator MarkHitpointDebug(Vector3 point)
	{
		GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		marker.AddComponent<SphereCollider>();
		marker.AddComponent<Rigidbody>();
		marker.transform.position = point;
		yield return new WaitForSeconds(0.5f);
		Destroy(marker);

	}
    //创建并调度工作，保存工作句柄
	void IndentSnow(float force, Vector3[] worldPositions,ref HandledResult newHandledResult)
	{

		newHandledResult.contactpoints = new NativeArray<Vector3>
		(worldPositions, Allocator.TempJob);
		newHandledResult.initialVerts = new NativeArray<Vector3>
 	 	(vertices, Allocator.TempJob);
		newHandledResult.modifiedVerts = new NativeArray<Vector3>
 		(modifiedVertices, Allocator.TempJob);
        //创建一个工作并拷贝数据  
  		IndentationJob meshIndentationJob = new IndentationJob
 		{
			 contactPoints = newHandledResult.contactpoints,
			 initialVertices = newHandledResult.initialVerts,
			 modifiedVertices = newHandledResult.modifiedVerts,
			 force = force,
			 radius = radius
  		};
        //调度工作
  		JobHandle indentationJobhandle = meshIndentationJob.Schedule(newHandledResult.initialVerts.Length,newHandledResult.initialVerts.Length);
  		
		newHandledResult.jobHandle = indentationJobhandle;

		scheduledJobsList.Add(newHandledResult);//保存工作句柄
	}
    //刷新工作，强制工作完成
	void CompleteJob(HandledResult handle)
	{
		scheduledJobsList.Remove(handle);

		handle.jobHandle.Complete();
  
		handle.contactpoints.Dispose();
		handle.initialVerts.Dispose();
		handle.modifiedVerts.CopyTo(modifiedVertices);
		handle.modifiedVerts.Dispose();

		mesh.vertices = modifiedVertices;
		vertices = mesh.vertices;
		mesh.RecalculateNormals();
			
	}
}

struct HandledResult
{
	public JobHandle jobHandle;
	public NativeArray<Vector3> contactpoints;
	public NativeArray<Vector3> initialVerts;
  	public NativeArray<Vector3> modifiedVerts;
}
