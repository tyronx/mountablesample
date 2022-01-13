using System;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace mountablesample
{
    public class EntityBoat : Entity, IRenderer, IMountableSupplier
    {
        public EntityBoatSeat[] Seats;

        // current forward speed
        public double ForwardSpeed = 0.0;

        // current turning speed (rad/tick)
        public double AngularVelocity = 0.0;


        public override bool ApplyGravity
        {
            get { return true; }
        }

        public override bool IsInteractable
        {
            get { return true; }
        }


        public override float MaterialDensity
        {
            get { return 100f; }
        }


        public override double SwimmingOffsetY
        {
            get { return 0.45; }
        }

        public double RenderOrder => 0;
        public int RenderRange => 999;


        public Vec3f[] MountOffsets = new Vec3f[] { new Vec3f(-0.8f, 0.27f, 0), new Vec3f(0.5f, 0.27f, 0) };

        public EntityBoat()
        {
            Seats = new EntityBoatSeat[2];
            for (int i = 0; i < Seats.Length; i++) Seats[i] = new EntityBoatSeat(this, i, MountOffsets[i]);
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            (api as ICoreClientAPI)?.Event.RegisterRenderer(this, EnumRenderStage.Before, "boatsim");

            // The mounted entity will try to mount as well, but at that time, the boat might not have been loaded, so we'll try mounting on both ends. 
            foreach (var seat in Seats)
            {
                if (seat.PassengerEntityIdForInit != 0 && seat.Passenger == null)
                {
                    var entity = api.World.GetEntityById(seat.PassengerEntityIdForInit) as EntityAgent;
                    if (entity != null)
                    {
                        entity.TryMount(seat);
                    }
                }
            }
        }


        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            // Client side we update every frame for smoother turning
            updateBoatAngleAndMotion(dt);
        }


        public override void OnGameTick(float dt)
        {
            if (World.Side == EnumAppSide.Server)
            {
                updateBoatAngleAndMotion(dt);
            }

            base.OnGameTick(dt);
        }

        private void updateBoatAngleAndMotion(float dt)
        {
            var motion = SeatsToMotion(dt);

            // Add some easing to it
            ForwardSpeed += (motion.X - ForwardSpeed) * dt;
            AngularVelocity += (motion.Y - AngularVelocity) * dt;

            var pos = SidedPos;

            if (ForwardSpeed != 0.0)
            {
                pos.Motion.Set(pos.GetViewVector().Mul((float)-ForwardSpeed).ToVec3d());
            }

            if (AngularVelocity != 0.0)
            {
                pos.Yaw += (float)AngularVelocity;
            }
        }

        public virtual Vec2d SeatsToMotion(float dt)
        {
            // Ignore lag spikes
            dt = Math.Min(0.2f, dt);

            int seatsRowing = 0;

            double linearMotion = 0;
            double angularMotion = 0;

            foreach (var seat in Seats)
            {
                var controls = seat.controls;
                if (!controls.TriesToMove) continue;

                float str = ++seatsRowing == 1 ? 1 : 0.5f;

                if (controls.Left || controls.Right)
                {
                    float dir = controls.Left ? 1 : -1;
                    angularMotion += str * dir * dt;
                }

                if (controls.Forward || controls.Backward)
                {
                    float dir = controls.Forward ? 1 : -1;
                    linearMotion += str * dir * dt * 5f;
                }
            }

            return new Vec2d(linearMotion, angularMotion);
        }


        public bool IsMountedBy(Entity entity)
        {
            foreach (var seat in Seats)
            {
                if (seat.Passenger == entity) return true;
            }
            return false;
        }

        public Vec3f GetMountOffset(Entity entity)
        {
            foreach (var seat in Seats)
            {
                if (seat.Passenger == entity)
                {
                    var offs = seat.MountOffset;
                    return new Vec3f(
                        (float)(offs.X * -Math.Cos(Pos.Yaw) + offs.Z * Math.Sin(Pos.Yaw)),
                        offs.Y,
                        (float)(offs.X * Math.Sin(Pos.Yaw) - offs.Z * Math.Cos(Pos.Yaw))
                    );
                }
            }
            return null;
        }


        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode)
        {
            if (mode != EnumInteractMode.Interact)
            {
                return;
            }

            // sneak + click to remove boat
            if (byEntity.Controls.Sneak && IsEmpty())
            {
                ItemStack stack = new ItemStack(World.GetItem(Code));
                if (!byEntity.TryGiveItemStack(stack))
                {
                    World.SpawnItemEntity(stack, ServerPos.XYZ);
                }
                Die();
                return;
            }

            if (World.Side == EnumAppSide.Server)
            {
                Vec3d boatDirection = Vec3dFromYaw(ServerPos.Yaw);
                Vec3d hitDirection = hitPosition.Normalize();
                double hitDotProd = hitDirection.X * boatDirection.X + hitDirection.Z * boatDirection.Z;
                int seatNumber = hitDotProd > 0.0 ? 0 : 1;

                if (byEntity.MountedOn == null && Seats[seatNumber].Passenger == null)
                {
                    byEntity.TryMount(Seats[seatNumber]);
                }
            }
        }


        public static Vec3d Vec3dFromYaw(float yawRad)
        {
            return new Vec3d(Math.Cos(yawRad), 0.0, -Math.Sin(yawRad));
        }

        public override bool CanCollect(Entity byEntity)
        {
            return false;
        }

        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            base.ToBytes(writer, forClient);

            writer.Write(Seats.Length);
            foreach (var seat in Seats)
            {
                writer.Write(seat.Passenger?.EntityId ?? (long)0);
            }
        }

        public override void FromBytes(BinaryReader reader, bool fromServer)
        {
            base.FromBytes(reader, fromServer);

            int numseats = reader.ReadInt32();
            for (int i = 0; i < numseats; i++)
            {
                long entityId = reader.ReadInt64();
                Seats[i].PassengerEntityIdForInit = entityId;
            }
        }

        public bool IsEmpty()
        {
            return !Seats.Any(seat => seat.Passenger != null);
        }

        public void Dispose()
        {

        }
    }

}