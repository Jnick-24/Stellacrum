using Godot;
using System;
using System.Collections.Generic;

public partial class CubeGrid : RigidBody3D
{
	public Aabb size { get; private set; } = new Aabb();
	private readonly Dictionary<Vector3I, CubeBlock> CubeBlocks = new ();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		
	}

	public Aabb BoundingBox()
	{
		return new Aabb(Position + size.Position*2.5f - Vector3.One*1.25f, size.End*2.5f);
	}

	#region blocks

	public void AddBlock(RayCast3D ray, Vector3 rotation, string blockId)
	{
		AddBlock(ray.GetCollisionPoint() + ray.GetCollisionNormal(), rotation, blockId);
	}

	public void AddBlock(Vector3 globalPosition, Vector3 rotation, string blockId)
	{
		AddBlock(GlobalToBlockCoord(globalPosition), rotation, blockId);
	}

	public void AddBlock(Vector3I position, Vector3 rotation, string blockId)
	{
		AddBlock(position, rotation, CubeBlockLoader.FromId(blockId).Copy());
	}

	public void AddBlock(Vector3I position_GridLocal, Vector3 rotation, CubeBlock block)
	{
		if (!CubeBlocks.ContainsKey(position_GridLocal))
		{
			AddChild(block);
			CubeBlocks.Add(position_GridLocal, block);

			block.Position = (Vector3) position_GridLocal*2.5f;
			block.GlobalRotation = rotation;

			block.collisionId = CreateShapeOwner(this);
			ShapeOwnerAddShape(block.collisionId, block.collision);
			ShapeOwnerSetTransform(block.collisionId, block.Transform);

			RecalcSize();
		}
		else
		{
			DebugDraw.Text("Failed to place block @ " + position_GridLocal, 5);
		}
	}

	public void RemoveBlock(RayCast3D ray)
	{
		RemoveBlock(ray.GetCollisionPoint() - ray.GetCollisionNormal());
	}

	public void RemoveBlock(Vector3 globalPosition)
	{
		RemoveBlock(GlobalToBlockCoord(globalPosition));
	}

	public void RemoveBlock(Vector3I position)
	{
		if (CubeBlocks.ContainsKey(position))
		{
			RemoveShapeOwner(CubeBlocks[position].collisionId);
			RemoveChild(CubeBlocks[position]);
			CubeBlocks.Remove(position);

			RecalcSize();
		}
	}

	private void RecalcSize()
	{
		Vector3I max = Vector3I.Zero;
		Vector3I min = Vector3I.Zero;

		foreach (var pos in CubeBlocks.Keys)
		{
			Aabb block = CubeBlocks[pos].Size(pos);
			
			if (block.End.X > max.X)
				max.X = (int) block.End.X;
			if (block.End.Y > max.Y)
				max.Y = (int) block.End.Y;
			if (block.End.Z > max.Z)
				max.Z = (int) block.End.Z;

			if (block.Position.X < min.X)
				min.X = (int) block.Position.X;
			if (block.Position.Y < min.Y)
				min.Y = (int) block.Position.Y;
			if (block.Position.Z < min.Z)
				min.Z = (int) block.Position.Z;
			
		}

		size = new Aabb(min, max);
	}

	public Vector3 RoundGlobalCoord(Vector3 global)
	{
		return ToGlobal((Vector3) GlobalToBlockCoord(global) * 2.5f);
	}

	public Vector3 PlaceProjectionGlobal(Vector3 from, Vector3 to)
	{
		Aabb box = BoundingBox();

		for (int i = 0; i < 8; i++)
			DebugDraw.Point(box.GetEndpoint(i), 0.5f, Colors.Red);

		Vector3 lTo = RoundGlobalCoord(to);
		lTo = RoundGlobalCoord(lTo.MoveToward(from, 1.25f));
		return lTo;
	}

	public Vector3 PlaceProjectionGlobal(RayCast3D ray)
	{
		Aabb box = BoundingBox();

		for (int i = 0; i < 8; i++)
			DebugDraw.Point(box.GetEndpoint(i), 0.5f, Colors.Red);

		return RoundGlobalCoord(ray.GetCollisionPoint() + ray.GetCollisionNormal());
	}

	public Vector3I GlobalToBlockCoord(Vector3 global)
	{
		if (!IsInsideTree())
			return Vector3I.Zero;
			
		return LocalToBlockCoord(ToLocal(global));
	}

	public Vector3I LocalToBlockCoord(Vector3 local)
	{
		return (Vector3I) (local/2.5f).Round();
	}

	#endregion

	public void Close()
	{
		foreach (var block in CubeBlocks)
			RemoveShapeOwner(block.Value.collisionId);
	}
}
