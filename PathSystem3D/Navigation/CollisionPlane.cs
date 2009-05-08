using System;
using System.Threading;
using OpenMetaverse;
using PathSystem3D.Mesher;

#if COLLIDER_ODE
using THIRDPARTY.OpenSim.Framework;
using THIRDPARTY.OpenSim.Region.Physics.Manager;
using THIRDPARTY.OpenSim.Region.Physics.OdePlugin;
#endif

namespace PathSystem3D.Navigation
{
    /// <summary>
    /// this is the 2D object for pathfinders like A*
    /// </summary>
    public class CollisionPlane
    {

        static int TotalCollisionPlanes = 0;
        readonly internal SimPathStore PathStore;
        readonly int MaxXPt, MaxYPt;
        private bool _UsePotentialFields = true;

        public bool UsePotentialFields
        {
            get { return _UsePotentialFields; }
            set
            {
                if (!_UsePotentialFields == value)
                {
                    _UsePotentialFields = value;
                    MatrixNeedsUpdate = true;
                }
            }
        }
        private bool _AdjacentBlocking = true;

        public bool AdjacentBlocking
        {
            get { return _AdjacentBlocking; }
            set
            {
                if (!_AdjacentBlocking == value)
                {
                    _AdjacentBlocking = value;
                    MatrixNeedsUpdate = true;
                }
            }
        }

        public CollisionPlane(int xsize0, int ysize0, float minZ, SimPathStore pathStore)
        {
            TotalCollisionPlanes++;
            PathStore = pathStore;
            MaxXPt = xsize0 - 1;
            MaxYPt = ysize0 - 1;
            MinZ = minZ;
            MaxZ = minZ + 3f;
        }

        public float MinZ { get; set; }
        public float WalkZLevel
        {
            //get { return _ZLevelMin; }
            set
            {
                if (value > MaxZ)
                {                   
                    Console.WriteLine("Resizing {0} < {1} ", this, value);
                    HeightMapNeedsUpdate = true;
                    MaxZ = value;
                }
                else if (value < MinZ)
                {
                      Console.WriteLine("Resizing {0} > {1} > ", this, value);
                      HeightMapNeedsUpdate = true;
                    MinZ = value;
                }
                if ((MaxZ - MinZ) > 5)
                {
                    //throw new ArgumentException(String.Format("Resizing {0} > {1} > ", this, value));
                }
            }
        }

        internal bool Accepts(float Z)
        {
            if (Z < MinZ + 3.3f)
            {
                if (Z >= MinZ-0.3f) return true;
                //if (MaxZ - Z > 3) return false;
            }
            return false;
        }

        public bool MatrixNeedsUpdate { get; set; }
        public bool HeightMapNeedsUpdate { get; set; }
        
        public delegate int NeighborPredicateDelegate(int NX,int XY);
        public int NeighborPredicate(int ix, int iy, int circleSize, NeighborPredicateDelegate param1)
        {
            int count = 0;
            int startx = ix - circleSize;
            int starty = iy - circleSize;
            int endx = ix + circleSize;
            int endy = iy + circleSize;
            if (startx < 0) startx = 0; else if (startx > MaxXPt) startx = MaxXPt;
            if (starty < 0) starty = 0; else if (starty > MaxYPt) starty = MaxYPt;
            if (endx < 0) endx = 0; else if (endx > MaxXPt) endx = MaxXPt;
            if (endy < 0) endy = 0; else if (endy > MaxYPt) endy = MaxYPt;
            for (int x = startx; x <= endx; x++)
                for (int y = starty; y <= endy; y++)
                {
                    if (y != iy && x != ix)
                        count += param1(x, y);
                }
            return count;
        }

        public override string ToString()
        {
            return MinZ + "-" + MaxZ + " @ " + BumpConstraint + "f";
        }
        byte[,] _BM;

        public float MaxZ { get; set; }

        public byte[,] ByteMatrix
        {
            get
            {
                if (_BM == null)
                {
                    MatrixNeedsUpdate = true;
                    _BM = new byte[MaxXPt+1, MaxYPt+1];
                    for (int y = MaxYPt; y >= 0; y--)
                        for (int x = MaxXPt; x >= 0; x--)
                            _BM[x, y] = SimPathStore.INITIALLY;
                }
                return _BM;
            }

        }
       
#if COLLIDER_ODE

        void ComputeLandingHeights()
        {
            float fromZ = MaxZ + 10;
            float StepSize = PathStore.StepSize; //0.2f
            int MAPSPACE = PathStore.MAPSPACE;
            int MAPSPACE1 = MAPSPACE - 1; // 1279
            bool needsInit = false;
            float[,] _HeightMap = HeightMap;

            if (LandingHieghts == null)
            {
                if (_HeightMap != null)
                {
                    LandingHieghts = (float[,])_HeightMap.Clone();
                }
                else
                {
                    LandingHieghts = new float[MAPSPACE, MAPSPACE];
                    needsInit = true;
                }
            }

            CollisionIndex[,] MeshIndex = PathStore.MeshIndex;
            // FallingPrims = new FallingPrim[MAPSPACE, MAPSPACE];
            float fy = 256.1f;
            OdeScene ps = PathStore.odeScene;
            PrimitiveBaseShape newcube = PrimitiveBaseShape.CreateBox();
            uint localId = 6666666;
            PhysicsVector position = new PhysicsVector(1,1,30);
            OdePrim oprim = (OdePrim)ps.AddPrimShape("FallingPrim_" + localId, newcube, position, new PhysicsVector(0.1f, 0.1f, 2f), Quaternion.Identity, true);
            oprim.LocalID = localId + 100000;
            oprim.SubscribeEvents(30000);

            for (int y = MAPSPACE1; y >= 0; y--)
            {
                fy = fy - StepSize;
                position.Y = fy;
                float fx = 256.1f;
                for (int x = MAPSPACE1; x >= 0; x--)
                {
                    fx = fx - StepSize;
                    position.X = fx;
                    if (needsInit) LandingHieghts[x, y] = float.MinValue;
                    if (MeshIndex[x, y] == null) continue;
                    float z = MinZ;
                    bool FoundClearZ = false;
                    while (z < MaxZ && !FoundClearZ)
                    {
                        float ClearZ = z;
                        position.Z = z;
                        if (!ps.IsSomethingAt(oprim))
                        {
                            FoundClearZ = true;
                            float CapZ = 2f + z;
                            while (z < CapZ && FoundClearZ)
                            {
                                if (ps.IsSomethingAt(oprim))
                                 {
                                     FoundClearZ = false;
                                     break;
                                 } 
                            }
                            FoundClearZ = true;
                            break;
                        }
                        z += 0.1f;
                    }
                    if (FoundClearZ)
                    {
                        LandingHieghts[x, y] = z;
                    }
                    //FallingPrims[x, y] = new
                }
            }
            _HeightMap = LandingHieghts;
        }

        internal float[,] LandingHieghts;
        //internal FallingPrim[,] FallingPrims;
        internal uint fallingPrims = 0;
        void ComputeLandingHeightsOld()
        {
            float fromZ = MaxZ + 10;
            float StepSize = PathStore.StepSize; //0.2f
            int MAPSPACE = PathStore.MAPSPACE;
            int MAPSPACE1 = MAPSPACE - 1; // 1279
            bool needsInit = false;
            float[,] _HeightMap = HeightMap;

            if (LandingHieghts == null)
            {
                if (_HeightMap != null)
                {
                    LandingHieghts = (float[,])_HeightMap.Clone();
                }
                else
                {
                    LandingHieghts = new float[MAPSPACE, MAPSPACE];
                    needsInit = true;
                }
            }
            CollisionIndex[,] MeshIndex = PathStore.MeshIndex;
            // FallingPrims = new FallingPrim[MAPSPACE, MAPSPACE];
            float fy = 256.1f;
            OdeScene ps = PathStore.odeScene;
            fallingPrims = 0;
            for (int y = MAPSPACE1; y >= 0; y--)
            {
                fy = fy - StepSize;
                float fx = 256.1f;
                for (int x = MAPSPACE1; x >= 0; x--)
                {
                    fx = fx - StepSize;
                    if (needsInit) LandingHieghts[x, y] = float.MinValue;
                    if (MeshIndex[x, y] == null) continue;
                    //FallingPrims[x, y] = new
                    FallingPrim(ps, this, new PhysicsVector(fx, fy, fromZ), x, y, 0f);
                    fallingPrims++;
                }

                int MaxTries = 100;
                while (fallingPrims > 0 && MaxTries-- > 0)
                {
                    //   Console.WriteLine("fallingPrims=" + fallingPrims);
                    ps.Simulate(0.133f);
                }
                //Console.WriteLine("fallingPrims left over {0} MaxTries Left over = {1}", fallingPrims, MaxTries);
                ps.Simulate(0.133f); // for removal of remainders or not needed?
                if (fallingPrims < 10)
                {
                    _HeightMap = LandingHieghts;
                }
                if (fallingPrims != 0)
                {
                    Console.WriteLine("fallingPrims left over {0} MaxTries Left over = {1}", fallingPrims, MaxTries);
                }
                else if (y % 100 == 0)

                    Console.WriteLine("Y={0} MaxTries Left over = {1}", y, MaxTries);
            }
        }
        //public class FallingPrim
        //{
             //public bool DoneMoving = false;
             //public uint localId;

            public void FallingPrim(OdeScene ps, CollisionPlane Plane, PhysicsVector position, int x, int y, float offsetZ)
            {
                uint localId = Plane.fallingPrims;
                PrimitiveBaseShape newcube = PrimitiveBaseShape.CreateBox();
                OdePrim oprim = (OdePrim)ps.AddPrimShape("FallingPrim_" + localId, newcube, position, new PhysicsVector(0.1f, 0.1f, 2f), Quaternion.Identity, true);
                oprim.LocalID = localId + 100000;
                oprim.OnCollisionUpdate += delegate(EventArgs args)
                {
                    if (!oprim.m_taintremove)
                    {
                       // CollisionEventUpdate arg = (CollisionEventUpdate)args;
                        //simhinfo 58 58 30
                       // DoneMoving = true;
                        LandingHieghts[x, y] = oprim.Position.Z + offsetZ;
                        fallingPrims--;
                        ps.remCollisionEventReporting(oprim);
                        ps.RemovePrim(oprim);
                    }
                };
                oprim.SubscribeEvents(30000);
            }
        //}
#endif

        float[,] _HeightMap;
        public float[,] HeightMap
        {
            get
            {
                if (_HeightMap == null)
                {
                    _HeightMap =(float[,]) PathStore.GroundPlane.Clone();
                    HeightMapNeedsUpdate = true;
                }
                return _HeightMap;
            }
        }

        //public byte this[int x, int y]
        //{
        //    get { return ByteMatrix[x, y]; }
        //    set { ByteMatrix[x, y] = value; }
        //}

        private float BumpConstraint = 0.6f;
        private float BumpConstraintPurple =  0.4f;

        public byte DefaultCollisionValue(int x, int y, float BumpConstraint, float[,] GroundPlane, byte b, float[,] Heights, CollisionIndex[,] cI)
        {
            if (b == SimPathStore.STICKY_PASSABLE) return b;
            float ZLevel = Heights[x, y];
            float GLevel = GroundPlane[x, y];
            if (ZLevel < GLevel) ZLevel = GLevel;

            int bumps = NeighborBump(x, y, ZLevel, MaxZ, ZLevel, BumpConstraint, Heights);
            if (bumps > 0)
                return SimPathStore.BLOCKED;

            if (BumpConstraintPurple > 0.6f)
            {
                bumps = NeighborBump(x, y, ZLevel, MaxZ, ZLevel, BumpConstraintPurple, Heights);
                if (bumps > 0)
                    return SimPathStore.BLOCK_PURPLE;
            }

            float Water = PathStore.WaterHeight;
            if (DiffLessThan(Water, ZLevel, 0.1f))
            {
                return SimPathStore.WATER_Z;
            }
            if (DiffLessThan(Water, PathStore.GroundPlane[x, y], 0.1f))
            {
                return SimPathStore.WATER_G;
            }
           
            float MaxZLevel = MaxZ;
            if (MaxZLevel <= ZLevel - 2)
            {
                return SimPathStore.TOO_HIGH;
            }
            if (ZLevel + 20 < MaxZLevel) // needs passable
                return SimPathStore.TOO_LOW;


            CollisionIndex c = cI[x, y];
            if (c != null)
            {
                return c.GetOccupiedValue(ZLevel,ZLevel);
            }

            bumps = NeighborBump(x, y, ZLevel, MaxZ, ZLevel, 0.1f, Heights);
            if (bumps > 0)
                return SimPathStore.BLOCKED_YELLOW;

            if (b != SimPathStore.BLOCKED)
                return SimPathStore.INITIALLY;


            return b;
        }

        internal void EnsureUpdated()
        {
            RenderHeightMap();
            if (!MatrixNeedsUpdate) return;
            {               
                CollisionIndex[,] MeshIndex = PathStore.MeshIndex;
                MatrixNeedsUpdate = false;
                byte[,] ToMatrix = ByteMatrix;
                float[,] Heights = HeightMap;
                float[,] GroundPlane = PathStore.GroundPlane;

                Console.WriteLine("\nStart UpdateCollisionPlane: {0} for {1}", PathStore, this);
                for (int y = MaxYPt - 1; y > 0; y--)
                {
                    for (int x = MaxXPt - 1; x > 0; x--)
                    {
                        ToMatrix[x, y] = DefaultCollisionValue(x, y, BumpConstraint, GroundPlane, ToMatrix[x, y], Heights, MeshIndex);
                    }
                }

                if (UsePotentialFields)
                {
                    AddPotentialFields(ToMatrix);
                }
                if (AdjacentBlocking)
                {
                   // AddAdjacentBlocking(ToMatrix, SimPathStore.MAYBE_BLOCKED, 3, SimPathStore.BLOCKED);
                   // AddEdgeBlocking(ToMatrix);
                }
                Console.WriteLine("\nEnd UpdateCollisionPlane: {0} for {1}", PathStore, this);
            }
        }

        void AddPotentialFields(byte[,] ToMatrix)
        {
            byte FeildEffect = SimPathStore.BLOCKED;
            AddFieldEffects(ToMatrix, ToMatrix, FeildEffect, 1, 1);
            AddFieldEffects(ToMatrix, ToMatrix, --FeildEffect, 5, 30);

            FeildEffect = SimPathStore.BLOCKED_YELLOW;
            AddFieldEffects(ToMatrix, ToMatrix, FeildEffect, 1, 1);
            AddFieldEffects(ToMatrix, ToMatrix, --FeildEffect, 5, 30);

            FeildEffect = SimPathStore.BLOCK_PURPLE;
            AddFieldEffects(ToMatrix, ToMatrix, FeildEffect, 1, 1);
            AddFieldEffects(ToMatrix, ToMatrix, --FeildEffect, 5, 30);
        }

        static bool DiffLessThan(float A, float B, float D)
        {
            return Math.Abs(A - B) <= D;
        }

        public static byte NeighborBump(int PX, int PY, float low, float high, float originZ, float mostDiff, float[,] heights)
        {
            float O;

            byte found = 0;
            O = NeighborLevel(low, high, originZ, PX, PY + 1, heights);
            if (!DiffLessThan(O, originZ, mostDiff)) found++;

            O = NeighborLevel(low, high, originZ, PX + 1, PY + 1, heights);
            if (!DiffLessThan(O, originZ, mostDiff)) found++;

            O = NeighborLevel(low, high, originZ, PX + 1, PY, heights);
            if (!DiffLessThan(O, originZ, mostDiff)) found++;

            O = NeighborLevel(low, high, originZ, PX + 1, PY - 1, heights);
            if (!DiffLessThan(O, originZ, mostDiff)) found++;

            O = NeighborLevel(low, high, originZ, PX, PY - 1, heights);
            if (!DiffLessThan(O, originZ, mostDiff)) found++;

            O = NeighborLevel(low, high, originZ, PX - 1, PY - 1, heights);
            if (!DiffLessThan(O, originZ, mostDiff)) found++;

            O = NeighborLevel(low, high, originZ, PX - 1, PY, heights);
            if (!DiffLessThan(O, originZ, mostDiff)) found++;

            O = NeighborLevel(low, high, originZ, PX - 1, PY + 1, heights);
            if (!DiffLessThan(O, originZ, mostDiff)) found++;

            return found;
        }

        internal static float NeighborLevel(float low, float high,float originZ, int PX, int PY, float[,] heights)
        {
            float n = heights[PX, PY];
          //  if (n < originZ) return originZ;
            return n;
            //CollisionIndex WP = PathStore.MeshIndex[PX, PY];
            //if (WP != null) return WP.GetZLevel(low, high);
            //float x = PX / PathStore.POINTS_PER_METER;
            //float y = PY / PathStore.POINTS_PER_METER;
            //float GL = GetSimRegion().GetGroundLevel(x, y);
            //float CPL = low;
            //return (GL > CPL) ? GL : CPL;

        }


        internal void RenderHeightMap()
        {
#if COLLIDER_ODE
                ComputeLandingHeights();
#else
//todo is silly to lock?            lock (this)
            if (!Monitor.TryEnter(this, 10))
            {
                SimPathStore.Debug("Someone is locking? " + this);
            }
            lock (this)
            {
                if (!HeightMapNeedsUpdate) return;
                _HeightMap = null;
                RenderGroundPlane();
                Console.WriteLine("\nStart RenderHeightMap: {0} for {1}", PathStore, this);
                CollisionIndex[,] MeshIndex = PathStore.MeshIndex;
                HeightMapNeedsUpdate = false;
                MatrixNeedsUpdate = true;
                //byte[,] ToMatrix = ByteMatrix;
                float[,] Heights = HeightMap;
                float[,] GroundPlane = PathStore.GroundPlane;
                PathStore.TaintMatrix();
                for (int y = MaxYPt; y >= 0; y--)
                {
                    for (int x = MaxXPt; x >= 0; x--)
                    {
                        Heights[x, y] = DefaultHeight(y, x, GroundPlane, Heights, MeshIndex);
                    }
                }
            }
#endif
            Console.WriteLine("\nEnd RenderHeightMap: {0} for {1}", PathStore, this);
        }


        public float DefaultHeight(int y, int x, float[,] GroundPlane, float[,] Heights, CollisionIndex[,] MeshIndex)
        {
            float testPlane = GroundPlane[x, y];

            if (testPlane + 2f < MinZ)
            {
                testPlane = MinZ - 2;
            }
            Heights[x, y] = testPlane;

            CollisionIndex W = MeshIndex[x, y];
            if (W == null) return testPlane;

            float level;
            if (W.OpenCapsuleAt(testPlane, testPlane + 16f, CollisionIndex.CapsuleZ, out level))
            {
                if (level < testPlane)
                {
                    level = testPlane;
                }
                return level;
            }
            return level;
            //else
            //{
            //    Heights[x, y] = W.GetZLevel(testPlane, testPlane + 16f);
            //}
        }


        private void RenderGroundPlane()
        {
            float LowestCared = MinZ - 2f;
            float[,] heights = HeightMap;
            float[,] GLevels = PathStore.GroundPlane;
            Console.WriteLine("\nStart RenderGroundPlane: {0} for {1} LowestCared={2}", PathStore, this, LowestCared);
            for (int y = MaxYPt; y >= 0; y--)
            {
                for (int x = MaxXPt; x >= 0; x--)
                {
                    float ZLevel = heights[x, y];
                    float GLevel = GLevels[x, y];
                    if (ZLevel < GLevel)
                    {
                        heights[x, y] = GLevel;
                        ZLevel = GLevel;
                    }
                   // if (ZLevel < LowestCared)
                     //   heights[x, y] = LowestCared;
                }
            }
            Console.WriteLine("\nEnd RenderGroundPlane: {0} for {1}", PathStore, this);
        }
     
        void AddFieldEffects(byte[,] from, byte[,] to, byte when, int iterations, byte step)
        {
            byte fronteer = when;
            int xsizem1 = MaxXPt - 1;
            while (iterations-- > 0)
            {
                byte self = (byte) (fronteer - step);
                for (int y = MaxYPt - 1; y > 0; y--)
                {
                    for (int x = xsizem1; x > 0; x--)
                    {
                        byte b = from[x, y];
                        if (b > 2 && b < fronteer)
                        {
                            if (SimPathStore.Special(b)) continue;
                            if (SurroundingBlocked0(x, y, fronteer, from) > 1)
                                to[x, y] = self;
                        }
                    }
                }
                fronteer -= step;
            }
        }

        private void AddAdjacentBlocking(byte[,] to, byte when, int req, byte then)
        {
            byte[,] from =(byte[,]) to.Clone();
            int xsizem1 = MaxXPt - 1;
            for (int y = MaxYPt - 1; y > 0; y--)
            {
                for (int x = xsizem1; x > 0; x--)
                {
                    byte b = from[x, y];
                    if (b == when)
                        if (SurroundingBlocked0(x, y, then, from) > req)
                            to[x, y] =  then;                        
                }
            }
            from = null;
        }
        private void AddEdgeBlocking(byte[,] to)
        {
            int xsizem1 = MaxXPt - 1;
            int xsizem2 = MaxXPt - 2;
            int ysizem1 = MaxYPt - 1;
            int ysizem2 = MaxYPt - 2;
            for (int y = ysizem1; y >= 0; y--)
            {
                to[0, y] = to[1, y];
                to[xsizem1, y] = to[xsizem2, y];
            }
            for (int x = xsizem1; x >= 0; x--)
            {
                to[x, 0] = to[x, 1];
                to[x,ysizem1] = to[x,ysizem2];
            }
        }

        public int SurroundingBlocked(int PX, int PY, byte[,] ZMatrix)
        {
            if (PX < 1 || PY < 1 || PX > MaxXPt - 1 || PY > MaxYPt - 1) return 0;
            return SurroundingBlocked0(PX, PY, SimPathStore.BLOCKED, ZMatrix);
        }

        /// <summary>
        ///  Private due to unchecked index
        /// </summary>
        /// <param name="PX"></param>
        /// <param name="PY"></param>
        /// <returns></returns>
        private static int SurroundingBlocked0(int PX, int PY, byte someValue, byte[,] ZMatrix)
        {
            int found = 0;
            byte O;

            O = ZMatrix[PX, PY + 1];
            if (O == someValue) found++;

            O = ZMatrix[PX + 1, PY + 1];
            if (O == someValue) found++;

            O = ZMatrix[PX + 1, PY];
            if (O == someValue) found++;

            O = ZMatrix[PX + 1, PY - 1];
            if (O == someValue) found++;

            O = ZMatrix[PX, PY - 1];
            if (O == someValue) found++;

            O = ZMatrix[PX - 1, PY - 1];
            if (O == someValue) found++;

            O = ZMatrix[PX - 1, PY];
            if (O == someValue) found++;

            O = ZMatrix[PX - 1, PY + 1];
            if (O == someValue) found++;

            return found;
        }

        internal void SetDefaultConstraints()
        {
            MatrixNeedsUpdate = true;
            HeightMapNeedsUpdate = true;
            _BM = null;
            BumpConstraint = 0.6f;
            BumpConstraintPurple = 0.4f;
        }

        private bool TightOrLooseNeedsUpdate;
        internal bool TightenConstraints()
        {
            if (TightOrLooseNeedsUpdate) return false;
            TightOrLooseNeedsUpdate = true;
            MatrixNeedsUpdate = true;
            _BM = null;
            BumpConstraint -= 0.05f;
            BumpConstraintPurple -= 0.05f;
            SimPathStore.Debug("\nTigthenConstraints " + this);
            if (BumpConstraint < 0.1)
            {
                SetDefaultConstraints();
               // return false;
            }
            EnsureUpdated();
            TightOrLooseNeedsUpdate = false;
            return true;
        }

        internal bool LoosenConstraints()
        {
            if (TightOrLooseNeedsUpdate) return false;
            TightOrLooseNeedsUpdate = true;
            MatrixNeedsUpdate = true;
            MatrixNeedsUpdate = true;
            _BM = null;
            BumpConstraint += 0.1f;
            BumpConstraintPurple += 0.1f;
            SimPathStore.Debug("\nLoosenConstraints " + this);
            if (BumpConstraint > 2)
            {
                SetDefaultConstraints();
            }

            EnsureUpdated();
            TightOrLooseNeedsUpdate = false;
            return true;
        }

        public bool Overlaps(float min, float max)
        {
            if (MinZ > max) return false;
            if (MaxZ < min) return false;
            return true;
        }

        internal void Refresh(Box3Fill changed, float BumpConstraint)
        {
            byte[,] ToMatrix = ByteMatrix;
            float[,] Heights = HeightMap;
            float[,] GroundPlane = PathStore.GroundPlane;
            CollisionIndex[,] MeshIndex = PathStore.MeshIndex;

            int xs = PathStore.ARRAY_X(changed.MinX);
            int xe = PathStore.ARRAY_X(changed.MaxX);
            int ys = PathStore.ARRAY_Y(changed.MinY);
            int ye = PathStore.ARRAY_Y(changed.MaxY);

            for (int x = xs; x <= xe; x++)
                for (int y = ys; y <= ye; y++)
                    Heights[x, y] = DefaultHeight(x, y, GroundPlane, Heights, MeshIndex);
            for (int x = xs; x <= xe; x++)
                for (int y = ys; y <= ye; y++)
                    ToMatrix[x, y] = DefaultCollisionValue(x, y, BumpConstraint, GroundPlane, ToMatrix[x, y],
                                                           Heights, MeshIndex);
        }
    }

}
