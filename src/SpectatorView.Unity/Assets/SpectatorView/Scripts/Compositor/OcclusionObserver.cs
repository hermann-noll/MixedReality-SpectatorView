using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.MixedReality.SpectatorView;
using Microsoft.MixedReality.PhotoCapture;
using System.IO;
using System.IO.Compression;
using System;

namespace SAP.MRS.SpectatorView
{
    [Serializable]
    public struct OcclusionFrame
    {
        public float timestamp;
        public Matrix4x4 intrinsics;
        public Matrix4x4 extrinsics;
        public Texture2D texture;
    }

    public class OcclusionObserver : MonoBehaviour
    {
        [SerializeField]
        private HolographicCameraObserver networkManager;

        [SerializeField]
        [Header("Runtime values")]
        private OcclusionFrame lastFrame;
        private Vector2[] depthIntrinsics = new Vector2[448 * 450];

        private static readonly string StreamDescriptionsPacketHeader = "STREAMDESCR";
        private static readonly string DepthCapturePacketHeader = "DEPTHCAPTURE";
        private static readonly string DepthFramePacketHeader = "DEPTHFRAME";
        private static readonly string DepthIntrinsicsPacketHeader = "DEPTHINTRINSICS";

        private void Awake()
        {
            networkManager.RegisterCommandHandler(StreamDescriptionsPacketHeader, OnStreamDescriptions);
            networkManager.RegisterCommandHandler(DepthFramePacketHeader, OnDepthFrame);
            networkManager.RegisterCommandHandler(DepthIntrinsicsPacketHeader, OnDepthIntrinsics);
        }

        private void OnStreamDescriptions(INetworkConnection connection, string command, BinaryReader reader, int remainingDataSize)
        {
            StreamDescription[] streamDescriptions = new StreamDescription[reader.ReadInt32()];
            for (int i = 0; i < streamDescriptions.Length; i++)
            {
                (streamDescriptions[i] = new StreamDescription()).Read(reader);
            }

            string message = $"Found {streamDescriptions.Length} stream descriptions";
            foreach (var descr in streamDescriptions)
            {
                message += $"\n\"{descr.SourceName}\" ({descr.SourceId}) {descr.Resolution.Width}x{descr.Resolution.Height}@{descr.Resolution.Framerate} {descr.CameraType}";
            }
            Debug.Log(message);
        }

        private void OnDepthFrame(INetworkConnection connection, string command, BinaryReader reader, int remainingDataSize)
        {
            lastFrame.timestamp = reader.ReadSingle();
            //lastFrame.intrinsics = reader.ReadMatrix4x4();
            lastFrame.extrinsics = reader.ReadMatrix4x4();
            if (lastFrame.texture == null)
                lastFrame.texture = new Texture2D(448, 450, TextureFormat.R16, false);

            byte[] uncompressed = new byte[448 * 450 * 2];
            byte[] compressed = reader.ReadBytes(reader.ReadInt32());
            using (var stream = new MemoryStream(compressed, false))
            using (var decompressor = new DeflateStream(stream, CompressionMode.Decompress))
            {
                decompressor.Read(uncompressed, 0, uncompressed.Length);
            }

            lastFrame.texture.SetPixelData(uncompressed, 0);
            Debug.Log("Got depth frame");
        }

        private void OnDepthIntrinsics(INetworkConnection connection, string command, BinaryReader reader, int remainingDataSize)
        {
            byte[] compressed = reader.ReadBytes(remainingDataSize);
            byte[] uncompressed = new byte[448 * 450 * 2 * 4];
            using (var stream = new MemoryStream(compressed, false))
            using (var decompressor = new DeflateStream(stream, CompressionMode.Decompress))
            {
                if (decompressor.Read(uncompressed, 0, uncompressed.Length) != uncompressed.Length)
                    throw new InvalidDataException("Compressed depth intrinsics are wrong");
            }
            
            for (int i = 0; i < depthIntrinsics.Length; i++)
            {
                depthIntrinsics[i].x = BitConverter.ToSingle(uncompressed, i * 8 + 0);
                depthIntrinsics[i].y = BitConverter.ToSingle(uncompressed, i * 8 + 4);
            }

            File.WriteAllText("depthintrinsics.json", string.Join("\n", depthIntrinsics.Select(v => $"{v.x}\t{v.y}")));
            Debug.Log("Got Depth intrinsics");
        }

        public void StartOcclusion()
        {
            if (!networkManager.IsConnected)
                return;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(DepthCapturePacketHeader);
                writer.Write(true); // I want depth frames
                writer.Write(true); // I want an intrinsics table

                var packet = stream.ToArray();
                networkManager.Broadcast(packet, 0, packet.LongLength);
            }
        }
    }
}
