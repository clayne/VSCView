﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExtendInput.Providers
{
    public class HidDevice : IDevice
    {
        public string DevicePath { get { return internalDevice.DevicePath; } }
        public int ProductId { get { return internalDevice.ProductID; } }
        public int VendorId { get { return internalDevice.VendorID; } }




        private HidSharp.HidDevice internalDevice;
        private bool IsOpen = false;
        private HidSharp.HidStream stream;

        public HidDevice(HidSharp.HidDevice internalDevice)
        {
            this.internalDevice = internalDevice;
        }
        private HidSharp.HidStream GetStream()
        {
            if (!IsOpen || stream == null)
                stream = internalDevice.Open();
            return stream;
        }

        public bool WriteReport(byte[] data)
        {
            try
            {
                GetStream().Write(data);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool WriteFeatureData(byte[] data)
        {
            try
            {
                GetStream().SetFeature(data);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool ReadFeatureData(out byte[] data, byte reportId = 0)
        {
            data = new byte[internalDevice.GetMaxFeatureReportLength()];
            try
            {
                data[0] = reportId;
                byte[] buffer = new byte[data.Length];
                GetStream().GetFeature(data);
                return true;
            }
            catch
            {
                return false;
            }
        }



        public void OpenDevice()
        {
            OpenDevice(DeviceMode.NonOverlapped, DeviceMode.NonOverlapped, ShareMode.ShareRead | ShareMode.ShareWrite);
        }

        public void OpenDevice(DeviceMode readMode, DeviceMode writeMode, ShareMode shareMode)
        {
            if (IsOpen) return;

            //HidSharp.OpenConfiguration config = new HidSharp.OpenConfiguration();
            //config.SetOption(HidSharp.OpenOption.Priority, HidSharp.OpenOption.Priority.)
            stream = internalDevice.Open();

            //_deviceReadMode = readMode;
            //_deviceWriteMode = writeMode;
            //_deviceShareMode = shareMode;

            IsOpen = true;
        }

        public void CloseDevice()
        {
            if (!IsOpen) return;
            stream.Close();
            IsOpen = false;
        }

        public void ReconnectDevice()
        {
            CloseDevice();
            OpenDevice();
        }

        public void Dispose()
        {
            //if (MonitorDeviceEvents) MonitorDeviceEvents = false;
            if (IsOpen) CloseDevice();
        }

        public string ReadSerialNumber()
        {
            return internalDevice.GetSerialNumber();
        }

        public void ReadReport(ReadReportCallback callback)
        {
            new Thread(() =>
            {
                try
                {
                    HidSharp.HidStream _stream = GetStream();
                    lock (_stream)
                    {
                        byte[] data = _stream.Read();
                        callback.Invoke(data.Skip(1).ToArray(), data[0]);
                    }
                }
                catch { }
            }).Start();
        }

        bool IEquatable<IDevice>.Equals(IDevice other)
        {
            Type typeThis = this.GetType().UnderlyingSystemType;
            Type typeOther = other.GetType().UnderlyingSystemType;

            if (typeThis.FullName != typeOther.FullName)
                return false;

            if (this.DevicePath != other.DevicePath)
                return false;

            return true;
        }

        public delegate void ReadReportCallback(byte[] report, int reportID);
    }

    public enum DeviceMode
    {
        NonOverlapped = 0,
        Overlapped = 1
    }

    [Flags]
    public enum ShareMode
    {
        Exclusive = 0,
        ShareRead = NativeMethods.FILE_SHARE_READ,
        ShareWrite = NativeMethods.FILE_SHARE_WRITE
    }

    internal static class NativeMethods
    {
        internal const short FILE_SHARE_READ = 0x1;
        internal const short FILE_SHARE_WRITE = 0x2;
    }
}