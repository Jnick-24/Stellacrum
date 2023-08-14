using Godot;
using System;
using System.Collections.Generic;

public partial class CubeGrid : RigidBody3D
{
	public Aabb Size { get; private set; } = new Aabb();
	private readonly Dictionary<Vector3I, CubeBlock> CubeBlocks = new ();
	public float Speed { get; private set; } = 0;
	public readonly List<CockpitBlock> Cockpits = new();

	public Vector3 MovementInput = Vector3.Forward;
	public Vector3 DesiredRotation = Vector3.Forward;

	private VectorPID thrustPid;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		thrustPid = new(1, 0, 0);
		AxisLockLinearX = true;
		AxisLockLinearY = true;
		AxisLockLinearZ = true;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (Input.IsActionJustPressed("Interact"))
			doRotate = !doRotate;
	}

	private bool doRotate = false;

	public override void _PhysicsProcess(double delta)
    {
		DebugDraw.Arrow(ToGlobal(CenterOfMass), DesiredRotation, 1, Colors.Blue);

		Vector3 pidOut = Vector3.Zero;

		if (doRotate)
			pidOut = thrustPid.Update(Basis * Vector3.Forward, DesiredRotation, (float) delta);

		DebugDraw.Arrow(ToGlobal(CenterOfMass), Basis * Vector3.Forward, 1, Colors.Red);


        foreach (var block in CubeBlocks.Values)
		{
			if (block is ThrusterBlock thrust)
			{
				Vector3 torque = (thrust.Position - CenterOfMass).Cross(thrust.ThrustForwardVector());
				DebugDraw.Arrow(thrust.GlobalPosition, torque, 0.5f, Colors.Green);

				float t = (torque * pidOut).Length();
				thrust.SetThrustPercent(t);
				DebugDraw.Text3D(t, thrust.GlobalPosition);
			}
		}

		Speed = LinearVelocity.Length();

		DebugDraw.Point(ToGlobal(CenterOfMass), 1, Colors.Yellow);

		DebugDraw.Text3D(Name + ": " + Mass, ToGlobal(CenterOfMass));
	}

	

	public Aabb BoundingBox()
	{
		return new Aabb(Position + Size.Position*2.5f - Vector3.One*1.25f, Size.End*2.5f);
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
			Mass += block.Mass;

			RecalcSize();
			RecalcMass();

			if (block is CockpitBlock c)
				Cockpits.Add(c);
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
			CubeBlock block = CubeBlocks[position];
			RemoveShapeOwner(block.collisionId);
			RemoveChild(block);
			Mass -= block.Mass;
			CubeBlocks.Remove(position);

			RecalcSize();
			RecalcMass();

			if (block is CockpitBlock c)
			{
				Cockpits.Remove(c);
			}
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

		Size = new Aabb(min, max);
	}

	private void RecalcMass()
	{
		Vector3 centerOfMass = Vector3.Zero;
		foreach (var block in CubeBlocks.Values)
			centerOfMass += block.Position * block.Mass;
		centerOfMass /= Mass;

		if (CenterOfMassMode != CenterOfMassModeEnum.Custom)
			CenterOfMassMode = CenterOfMassModeEnum.Custom;
		CenterOfMass = centerOfMass;
	}

	public Vector3 RoundGlobalCoord(Vector3 global)
	{
		if (IsInsideTree())
			return ToGlobal((Vector3) GlobalToBlockCoord(global) * 2.5f);
		else
			return global;
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
		{
			RemoveShapeOwner(block.Value.collisionId);
			block.Value.QueueFree();
		}
	}

	public bool IsEmpty()
	{
		return CubeBlocks.Count == 0;
	}
}
