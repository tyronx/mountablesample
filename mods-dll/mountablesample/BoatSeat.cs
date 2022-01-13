using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace mountablesample
{
    public class EntityBoatSeat : IMountable
    {
        public EntityBoat EntityBoat;

        public int SeatNumber;

        public Vec3f MountOffset;

        public EntityControls controls = new EntityControls();

        public EntityAgent Passenger = null;

        public long PassengerEntityIdForInit;

        public EntityBoatSeat(EntityBoat entityBoat, int seatNumber, Vec3f mountOffset)
        {
            controls.OnAction = this.onControls;
            this.EntityBoat = entityBoat;
            this.SeatNumber = seatNumber;
            this.MountOffset = mountOffset;
        }

        public static IMountable GetMountable(IWorldAccessor world, TreeAttribute tree)
        {
            Entity entityBoat = world.GetEntityById(tree.GetLong("entityIdBoat"));
            if (entityBoat is EntityBoat eBoat)
            {
                return eBoat.Seats[tree.GetInt("seatNumber")];
            }

            return null;
        }




        public Vec3d MountPosition
        {
            get
            {
                var pos = EntityBoat.SidedPos;

                return pos.XYZ.Add(
                    -MountOffset.X * Math.Cos(pos.Yaw) + MountOffset.Z * Math.Sin(pos.Yaw),
                    MountOffset.Y,
                    MountOffset.X * Math.Sin(pos.Yaw) - MountOffset.Z * Math.Cos(pos.Yaw)
                );
            }
        }

        public string SuggestedAnimation
        {
            get { return "sitflooridle"; }
        }

        public EntityControls Controls
        {
            get { return this.controls; }
        }

        public float? MountYaw
        {
            get { return this.EntityBoat.SidedPos.Yaw; }
        }

        public IMountableSupplier MountSupplier => EntityBoat;

        public void DidUnmount(EntityAgent entityAgent)
        {
            this.Passenger = null;
        }

        public void DidMount(EntityAgent entityAgent)
        {
            if (this.Passenger != null && this.Passenger != entityAgent)
            {
                this.Passenger.TryUnmount();
                return;
            }

            this.Passenger = entityAgent;
        }

        public void MountableToTreeAttributes(TreeAttribute tree)
        {
            tree.SetString("className", "boat");
            tree.SetLong("entityIdBoat", this.EntityBoat.EntityId);
            tree.SetInt("seatNumber", SeatNumber);
        }

        internal void onControls(EnumEntityAction action, bool on, ref EnumHandling handled)
        {
            if (action == EnumEntityAction.Sneak && on)
            {
                Passenger?.TryUnmount();
                controls.StopAllMovement();
            }
        }

    }



}