﻿using HidLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace VSCView
{
    public class DS4Controller : IController
    {
        public const int VendorId = 0x054C;
        public const int ProductIdDongle = 0x0BA0;
        public const int ProductIdWired = 0x09CC; // and BT

        public bool SensorsEnabled;
        private HidDevice _device;
        int stateUsageLock = 0, reportUsageLock = 0;

        #region DATA STRUCTS
        public enum VSCEventType
        {
            CONTROL_UPDATE = 0x01,
            CONNECTION_DETAIL = 0x03,
            BATTERY_UPDATE = 0x04,
        }

        public enum ConnectionState
        {
            DISCONNECT = 0x01,
            CONNECT = 0x02,
            PAIRING = 0x03,
        }

        public enum Melody : UInt32
        {
            Warm_and_Happy = 0x00,
            Invader = 0x01,
            Controller_Confirmed = 0x02,
            Victory = 0x03,
            Rise_and_Shine = 0x04,
            Shorty = 0x05,
            Warm_Boot = 0x06,
            Next_Level = 0x07,
            Shake_it_off = 0x08,
            Access_Denied = 0x09,
            Deactivate = 0x0a,
            Discovery = 0x0b,
            Triumph = 0x0c,
            The_Mann = 0x0d,
        }

        public enum EControllerType
        {
            Chell,
            ReleaseV1,
            ReleaseV2,
        }

        public ControllerState GetState()
        {
            if (0 == Interlocked.Exchange(ref stateUsageLock, 1))
            {
                ControllerState newState = (ControllerState)State.Clone();
                /*
                ControllerState newState = new ControllerState();
                newState.ButtonsOld = (SteamControllerButtons)State.ButtonsOld.Clone();

                newState.LeftTrigger = State.LeftTrigger;
                newState.RightTrigger = State.RightTrigger;

                newState.LeftStickX = State.LeftStickX;
                newState.LeftStickY = State.LeftStickY;
                newState.LeftPadX = State.LeftPadX;
                newState.LeftPadY = State.LeftPadY;
                newState.RightPadX = State.RightPadX;
                newState.RightPadY = State.RightPadY;

                newState.AccelerometerX = State.AccelerometerX;
                newState.AccelerometerY = State.AccelerometerY;
                newState.AccelerometerZ = State.AccelerometerZ;
                newState.AngularVelocityX = State.AngularVelocityX;
                newState.AngularVelocityY = State.AngularVelocityY;
                newState.AngularVelocityZ = State.AngularVelocityZ;
                newState.OrientationW = State.OrientationW;
                newState.OrientationX = State.OrientationX;
                newState.OrientationY = State.OrientationY;
                newState.OrientationZ = State.OrientationZ;

                //newState.DataStuck = State.DataStuck;
                */
                State = newState;
                Interlocked.Exchange(ref stateUsageLock, 0);
            }
            return State;
        }
        #endregion

        public EConnectionType ConnectionType => EConnectionType.Unknown;

        ControllerState State = new ControllerState();
        ControllerState OldState = new ControllerState();

        bool Initalized;

        public delegate void StateUpdatedEventHandler(object sender, ControllerState e);
        public event StateUpdatedEventHandler StateUpdated;
        protected virtual void OnStateUpdated(ControllerState e)
        {
            StateUpdated?.Invoke(this, e);
        }

        public DS4Controller(HidDevice device, EConnectionType ConnectionType = EConnectionType.Unknown)
        {
            State.Controls["quad_left"] = new ControlDPad();
            State.Controls["quad_right"] = new ControlButtonQuad(EOrientation.Diamond);
            State.Controls["bumpers"] = new ControlButtonPair();
            State.Controls["triggers"] = new ControlTriggerPair(HasStage2: false);
            State.Controls["menu"] = new ControlButtonPair();
            State.Controls["home"] = new ControlButton();
            State.Controls["stick_left"] = new ControlStick(HasClick: true);
            State.Controls["stick_right"] = new ControlStick(HasClick: true);
            State.Controls["touch"] = new ControlTouch(TouchCount: 2, HasClick: true);

            // According to this the normalized domain of the DS4 gyro is 1024 units per rad/s: https://gamedev.stackexchange.com/a/87178

            State.ButtonsOld = new SteamControllerButtons();

            _device = device;

            Initalized = false;
        }

        public void Initalize()
        {
            if (Initalized) return;

            // open the device overlapped read so we don't get stuck waiting for a report when we write to it
            _device.OpenDevice(DeviceMode.Overlapped, DeviceMode.NonOverlapped, ShareMode.ShareRead | ShareMode.ShareWrite);

            //_device.Inserted += DeviceAttachedHandler;
            //_device.Removed += DeviceRemovedHandler;

            //_device.MonitorDeviceEvents = true;

            Initalized = true;

            //_attached = _device.IsConnected;

            _device.ReadReport(OnReport);
        }

        public void DeInitalize()
        {
            if (!Initalized) return;

            //_device.Inserted -= DeviceAttachedHandler;
            //_device.Removed -= DeviceRemovedHandler;

            //_device.MonitorDeviceEvents = false;

            Initalized = false;
            _device.CloseDevice();
        }

        public void Identify()
        {
        }

        public bool CheckSensorDataStuck()
        {
            return (OldState != null &&
                State.AccelerometerX == 0 &&
                State.AccelerometerY == 0 &&
                State.AccelerometerZ == 0 ||
                State.AccelerometerX == OldState.AccelerometerX &&
                State.AccelerometerY == OldState.AccelerometerY &&
                State.AccelerometerZ == OldState.AccelerometerZ ||
                State.AngularVelocityX == OldState.AngularVelocityX &&
                State.AngularVelocityY == OldState.AngularVelocityY &&
                State.AngularVelocityZ == OldState.AngularVelocityZ
            );
        }

        public string GetName()
        {
            List<string> NameParts = new List<string>();

            byte[] ManufacturerBytes;
            _device.ReadManufacturer(out ManufacturerBytes); // Sony Interactive Entertainment
            string Manufacturer = System.Text.Encoding.Unicode.GetString(ManufacturerBytes)?.Trim('\0');
            NameParts.Add(Manufacturer);

            byte[] ProductBytes;
            _device.ReadProduct(out ProductBytes); // DUALSHOCK®4 USB Wireless Adaptor
            string Product = System.Text.Encoding.Unicode.GetString(ProductBytes)?.Trim('\0');
            NameParts.Add(Product);

            byte[] SerialNumberBytes;
            _device.ReadSerialNumber(out SerialNumberBytes); // DUALSHOCK®4 USB Wireless Adaptor
            string SerialNumber = System.Text.Encoding.Unicode.GetString(SerialNumberBytes)?.Trim('\0');
            if (string.IsNullOrWhiteSpace(SerialNumber))
            {
                NameParts.Add(_device.HardwareId);
            }
            else
            {
                NameParts.Add(SerialNumber);
            }

            return string.Join(@" | ", NameParts.Where(dr => !string.IsNullOrWhiteSpace(dr)).Select(dr => dr.Replace("&", "&&")));
        }

        private void OnReport(HidReport report)
        {
            if (!Initalized) return;

            if (0 == Interlocked.Exchange(ref reportUsageLock, 1))
            {
                OldState = State; // shouldn't this be a clone?
                //if (_attached == false) { return; }

                bool BT = report.ReportId == 17;
                int baseOffset = BT ? 2 : 0;

                (State.Controls["stick_left"] as ControlStick).X = (report.Data[baseOffset + 0] - 128) / 128f;
                (State.Controls["stick_left"] as ControlStick).Y = -(report.Data[baseOffset + 1] - 128) / 128f;
                (State.Controls["stick_right"] as ControlStick).X = (report.Data[baseOffset + 2] - 128) / 128f;
                (State.Controls["stick_right"] as ControlStick).Y = -(report.Data[baseOffset + 3] - 128) / 128f;

                (State.Controls["quad_right"] as ControlButtonQuad).Button0 = (report.Data[baseOffset + 4] & 128) == 128;
                (State.Controls["quad_right"] as ControlButtonQuad).Button1 = (report.Data[baseOffset + 4] & 64) == 64;
                (State.Controls["quad_right"] as ControlButtonQuad).Button2 = (report.Data[baseOffset + 4] & 32) == 32;
                (State.Controls["quad_right"] as ControlButtonQuad).Button3 = (report.Data[baseOffset + 4] & 16) == 16;

                switch ((report.Data[baseOffset + 4] & 0x0f))
                {
                    case 0: (State.Controls["quad_left"] as ControlDPad).Direction = EDPadDirection.North; break;
                    case 1: (State.Controls["quad_left"] as ControlDPad).Direction = EDPadDirection.NorthEast; break;
                    case 2: (State.Controls["quad_left"] as ControlDPad).Direction = EDPadDirection.East; break;
                    case 3: (State.Controls["quad_left"] as ControlDPad).Direction = EDPadDirection.SouthEast; break;
                    case 4: (State.Controls["quad_left"] as ControlDPad).Direction = EDPadDirection.South; break;
                    case 5: (State.Controls["quad_left"] as ControlDPad).Direction = EDPadDirection.SouthWest; break;
                    case 6: (State.Controls["quad_left"] as ControlDPad).Direction = EDPadDirection.West; break;
                    case 7: (State.Controls["quad_left"] as ControlDPad).Direction = EDPadDirection.NorthWest; break;
                    default: (State.Controls["quad_left"] as ControlDPad).Direction = EDPadDirection.None; break;
                }

                (State.Controls["stick_right"] as ControlStick).Click = (report.Data[baseOffset + 5] & 128) == 128;
                (State.Controls["stick_left"] as ControlStick).Click = (report.Data[baseOffset + 5] & 64) == 64;
                (State.Controls["menu"] as ControlButtonPair).Button1 = (report.Data[baseOffset + 5] & 32) == 32;
                (State.Controls["menu"] as ControlButtonPair).Button0 = (report.Data[baseOffset + 5] & 16) == 16;
                //State.ButtonsOld.RightTrigger = (report.Data[baseOffset + 5] & 8) == 8;
                //State.ButtonsOld.LeftTrigger = (report.Data[baseOffset + 5] & 4) == 4;
                (State.Controls["bumpers"] as ControlButtonPair).Button1 = (report.Data[baseOffset + 5] & 2) == 2;
                (State.Controls["bumpers"] as ControlButtonPair).Button0 = (report.Data[baseOffset + 5] & 1) == 1;

                // counter
                // bld.Append((report.Data[baseOffset + 6] & 0xfc).ToString().PadLeft(3, '0'));

                (State.Controls["home"] as ControlButton).Button0 = (report.Data[baseOffset + 6] & 0x1) == 0x1;
                State.ButtonsOld.DS4PadClick = (report.Data[baseOffset + 6] & 0x2) == 0x2;

                (State.Controls["triggers"] as ControlTriggerPair).Analog0 = (float)report.Data[baseOffset + 7] / byte.MaxValue;
                (State.Controls["triggers"] as ControlTriggerPair).Analog1 = (float)report.Data[baseOffset + 8] / byte.MaxValue;

                // GyroTimestamp
                //bld.Append(BitConverter.ToUInt16(report.Data, baseOffset + 9).ToString().PadLeft(5));

                // Battery Power Level
                //bld.Append(report.Data[baseOffset + 11].ToString("X2") + "   ");

                State.AngularVelocityX = BitConverter.ToInt16(report.Data, baseOffset + 12);
                State.AngularVelocityZ = BitConverter.ToInt16(report.Data, baseOffset + 14);
                State.AngularVelocityY = BitConverter.ToInt16(report.Data, baseOffset + 16);
                State.AccelerometerX = BitConverter.ToInt16(report.Data, baseOffset + 18);
                State.AccelerometerY = BitConverter.ToInt16(report.Data, baseOffset + 20);
                State.AccelerometerZ = BitConverter.ToInt16(report.Data, baseOffset + 22);

                // ??
                // bld.Append(report.Data[baseOffset + 27].ToString("X2"));

                //State.Inputs.? = (report.Data[baseOffset + 29] & 128) == 128;
                //State.Inputs.Mic = (report.Data[baseOffset + 29] & 64) == 64;
                //State.Inputs.Headphone = (report.Data[baseOffset + 29] & 32) == 32;
                //State.Inputs.PowerCable = (report.Data[baseOffset + 29] * 16) == 16;

                //int bat = report.Data[baseOffset + 29] & 0x0f;
                //bool plugged = (report.Data[baseOffset + 29] & 0x10) == 0x10;

                // ??
                // bld.Append(report.Data[baseOffset + 30].ToString("X2"));

                // TOUCH COUNTER
                // bld.Append(report.Data[baseOffset + 31].ToString("X2") + " ");

                int TouchDataCount = report.Data[baseOffset + 32];

                for (int FingerCounter = 0; FingerCounter < TouchDataCount; FingerCounter++)
                {
                    // Touch Pad Counter
                    // report.Data[baseOffset + 33 + FingerCounter * 8 + 0];

                    bool Finger1 = (report.Data[baseOffset + 33 + FingerCounter * 8 + 1] & 0x80) != 0x80;
                    byte Finger1Index = (byte)(report.Data[baseOffset + 33 + FingerCounter * 8 + 1] & 0x7f);
                    int F1X = report.Data[baseOffset + 33 + FingerCounter * 8 + 2 + 0] | ((report.Data[baseOffset + 33 + FingerCounter * 8 + 2 + 1] & 0xF) << 8);
                    int F1Y = ((report.Data[baseOffset + 33 + FingerCounter * 8 + 2 + 1] & 0xF0) >> 4) | (report.Data[baseOffset + 33 + FingerCounter * 8 + 2 + 2] << 4);

                    bool Finger2 = (report.Data[baseOffset + 37 + FingerCounter * 8 + 1] & 0x80) != 0x80;
                    byte Finger2Index = (byte)(report.Data[baseOffset + 37 + FingerCounter * 8 + 1] & 0x7f);
                    int F2X = report.Data[baseOffset + 37 + FingerCounter * 8 + 2 + 0] | ((report.Data[baseOffset + 37 + FingerCounter * 8 + 2 + 1] & 0xF) << 8);
                    int F2Y = ((report.Data[baseOffset + 37 + FingerCounter * 8 + 2 + 1] & 0xF0) >> 4) | (report.Data[baseOffset + 37 + FingerCounter * 8 + 2 + 2] << 4);
                }

                ControllerState NewState = GetState();
                OnStateUpdated(NewState);
                Interlocked.Exchange(ref reportUsageLock, 0);

                _device.ReadReport(OnReport);
            }
        }

        /*private void DeviceAttachedHandler()
        {
            lock (controllerStateLock)
            {
                _attached = true;
                Console.WriteLine("VSC Address Attached");
                _device.ReadReport(OnReport);
            }
        }

        private void DeviceRemovedHandler()
        {
            lock (controllerStateLock)
            {
                _attached = false;
                Console.WriteLine("VSC Address Removed");
            }
        }*/
    }

    public class DS4ControllerFactory : IControllerFactory
    {
        public IController[] GetControllers()
        {
            List<HidDevice> _devices = HidDevices.Enumerate(DS4Controller.VendorId, DS4Controller.ProductIdDongle, DS4Controller.ProductIdWired).ToList();
            List<DS4Controller> ControllerList = new List<DS4Controller>();
            string bt_hid_id = @"00001124-0000-1000-8000-00805f9b34fb";
            string wired_m = "&pid_09cc";
            string bt_m = "_pid&09cc";
            string dongle_m = "&pid_0ba0";

            for (int i = 0; i < _devices.Count; i++)
            {
                if (_devices[i] != null)
                {
                    HidDevice _device = _devices[i];
                    string devicePath = _device.DevicePath.ToString();

                    EConnectionType ConType = EConnectionType.Unknown;
                    switch (_device.Attributes.ProductId)
                    {
                        case DS4Controller.ProductIdWired:
                            if (devicePath.Contains(bt_hid_id))
                            {
                                ConType = EConnectionType.Bluetooth;
                            }
                            else
                            {
                                ConType = EConnectionType.USB;
                            }
                            break;
                        case DS4Controller.ProductIdDongle:
                            ConType = EConnectionType.Dongle;
                            break;
                    }

                    ControllerList.Add(new DS4Controller(_device, ConType));
                }
            }

            return ControllerList.OrderByDescending(dr => dr.ConnectionType).ThenBy(dr => dr.GetName()).ToArray();
        }
    }
}