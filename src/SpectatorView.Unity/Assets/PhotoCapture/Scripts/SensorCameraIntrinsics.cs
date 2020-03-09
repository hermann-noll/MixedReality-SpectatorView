using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using UnityEngine;

namespace SAP.MRS.PhotoCapture
{
    [ComImport]
    [Guid("9086e81c-0485-434d-918b-25924a877b09")]
    [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    public unsafe interface ISensorCameraIntrinsics
    {
        void MapImagePointToCameraUnitPlane(float* uv, float* xy);
    }

    public class SensorCameraIntrinsics
    {
        public static readonly Guid SensorStreaming_CameraIntrinsics = new Guid("5DCC7829-9471-4D78-8C27-5B2B9A0EC5EF");

        private ISensorCameraIntrinsics backend;

        public SensorCameraIntrinsics(ISensorCameraIntrinsics backend)
        {
            this.backend = backend;
        }
        
        public unsafe Vector2 MapImagePointToCameraUnitPlane(Vector2 uv)
        {
            var uvArray = new float[2] { uv.x, uv.y };
            var xyArray = new float[2] { float.NaN, float.NaN };
            fixed (float* uvArrayPtr = uvArray)
            fixed (float* xyArrayPtr = xyArray)
            {
                try
                {
                    backend.MapImagePointToCameraUnitPlane(uvArrayPtr, xyArrayPtr);
                }
                catch(COMException)
                {
                    xyArray[0] = xyArray[1] = float.NaN;
                }
            }
            return new Vector2(xyArray[0], xyArray[1]);
        }

        public Vector2[] CreateTable(Vector2Int size)
        {
            var table = new Vector2[size.x * size.y];
            for (int y = 0; y < size.y; y++)
                for (int x = 0; x < size.x; x++)
                    table[y * size.x + x] = MapImagePointToCameraUnitPlane(new Vector2(x, y));
            return table;
        }
    }
}
