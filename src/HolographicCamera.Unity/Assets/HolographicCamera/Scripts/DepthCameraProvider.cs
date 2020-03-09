using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Microsoft.MixedReality.SpectatorView;
using Microsoft.MixedReality.PhotoCapture;

namespace SAP.MRS.SpectatorView
{
    public class DepthCameraProvider : MonoBehaviour
    {
        private INetworkManager networkManager;
        private INetworkConnection currentConnection;
        private new HoloLensCamera camera = null;
        private System.Diagnostics.Stopwatch timestampWatch;
        private bool isCameraInitialized = false;
        private bool isCapturing = false;
        private bool shouldSendIntrinsicsTable = false;

        private static readonly string StreamDescriptionsPacketHeader = "STREAMDESCR";
        private static readonly string DepthCapturePacketHeader = "DEPTHCAPTURE";
        private static readonly string DepthFramePacketHeader = "DEPTHFRAME";
        private static readonly string DepthIntrinsicsPacketHeader = "DEPTHINTRINSICS";

        private static readonly string LongThrowToFStreamName = "Sensor Streaming";
        private static readonly string LongThrowToFStreamId = "Source#2";

        private void Awake()
        {
            networkManager = GetComponent<INetworkManager>();
            if (networkManager == null)
            {
                throw new MissingComponentException("Missing network manager component");
            }

            networkManager.RegisterCommandHandler(DepthCapturePacketHeader, OnDepthCapturePacket);
            networkManager.Connected += _ =>
            {
                timestampWatch = new System.Diagnostics.Stopwatch();
                timestampWatch.Start();
                SendStreamDescriptions();
            };
            camera = new HoloLensCamera(CaptureMode.Continuous, PixelFormat.L16);
            camera.OnCameraInitialized += OnCameraInitialized;
            camera.OnCameraStarted += OnCameraStarted;
            camera.OnFrameCaptured += OnFrameCaptured;
            Task.Run(() => camera.Initialize());
        }

        private void OnDestroy()
        {
            if (isCapturing)
                camera.StopContinuousCapture();
            camera?.Dispose();
            camera = null;
        }

        private void SendStreamDescriptions()
        {
            if (!isCameraInitialized)
                return;

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                var descriptions = camera.StreamSelector.StreamDescriptions;
                writer.Write(StreamDescriptionsPacketHeader);
                writer.Write(descriptions.Count);
                foreach (var descr in descriptions)
                    descr.Write(writer);

                var packet = stream.ToArray();
                networkManager.Broadcast(packet, 0, packet.LongLength);
            }
        }

        private void OnDepthCapturePacket(INetworkConnection connection, string command, BinaryReader reader, int remainingDataSize)
        {
            if (!isCameraInitialized)
                return;

            bool shouldBeCapturing = reader.ReadBoolean();
            shouldSendIntrinsicsTable = reader.ReadBoolean();

            if (shouldBeCapturing == isCapturing)
                return;
            isCapturing = shouldBeCapturing;

            if (shouldBeCapturing)
            {
                currentConnection = connection;
                var streamDescr = camera.StreamSelector.StreamDescriptions.Single(d =>
                    d.SourceName.Contains(LongThrowToFStreamName) &&
                    d.SourceId.Contains(LongThrowToFStreamId));
                camera.Start(streamDescr);
                Debug.Log("DepthCameraProvider: Starting to start camera");
            }
            else
            {
                camera.StopContinuousCapture();
                camera.Stop();
            }
        }

        private void OnCameraInitialized(HoloLensCamera sender, bool initializeSuccessful)
        {
            if (!initializeSuccessful)
            {
                Debug.LogError("DepthCameraProvider: Could not initialize HoloLens Camera");
                return;
            }
            Debug.Log("DepthCameraProvider: HoloLens Camera is initialized");
            isCameraInitialized = true;
            if (networkManager.IsConnected)
                SendStreamDescriptions();
        }

        private void OnCameraStarted(HoloLensCamera sender, bool startSuccessful)
        {
            if (!startSuccessful)
            {
                Debug.LogError("DepthCameraProvider: Could not start HoloLens Camera");
                isCapturing = false;
                return;
            }
            Debug.Log("DepthCameraProvider: HoloLens Camera is started");
            camera.StartContinuousCapture();
        }

        private void OnFrameCaptured(HoloLensCamera sender, CameraFrame frame)
        {
            if (currentConnection == null)
                return;

            if (shouldSendIntrinsicsTable)
                SendIntrinsicsTable(frame);

            byte[] compressedFrame;
            using (var stream = new MemoryStream())
            using (var compressor = new DeflateStream(stream, System.IO.Compression.CompressionLevel.Fastest, true))
            {
                compressor.Write(frame.PixelData, 0, frame.PixelData.Length);
                compressor.Flush();
                compressor.Close();
                compressedFrame = stream.ToArray();
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(DepthFramePacketHeader);
                writer.Write((float)timestampWatch.Elapsed.TotalSeconds);
                writer.Write(frame.Extrinsics.ViewFromWorld);
                writer.Write(compressedFrame.Length);
                writer.Write(compressedFrame);

                var packet = stream.ToArray();
                currentConnection.Send(packet, 0, packet.LongLength);
            }
        }

        private void SendIntrinsicsTable(CameraFrame frame)
        {
            var intrinsics = frame.SensorCameraIntrinsics;
            if (intrinsics == null)
                return;

            var table = intrinsics.CreateTable(new Vector2Int((int)frame.Resolution.Width, (int)frame.Resolution.Height));
            byte[] compressedTable;
            using (var stream = new MemoryStream(table.Length * sizeof(float)))
            using (var compressor = new DeflateStream(stream, System.IO.Compression.CompressionLevel.Fastest, true))
            using (var writer = new BinaryWriter(compressor))
            {
                foreach (var v in table)
                    writer.Write(v);
                compressor.Close();
                compressedTable = stream.ToArray();
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(DepthIntrinsicsPacketHeader);
                writer.Write(compressedTable);

                var packet = stream.ToArray();
                currentConnection.Send(packet, 0, packet.LongLength);
            }

            shouldSendIntrinsicsTable = false;
        }
    }
}
