﻿// Copyright (c) Microsoft. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Windows.Foundation;

namespace Microsoft.IoT.Devices.Input
{
    public sealed class Switch : ISwitch, IEventObserver, IDisposable
    {
        #region Member Variables
        private double debounceTimeout = 50;
        private bool initialized;
        private bool isOn = false;
        private GpioPinValue onValue = GpioPinValue.High;
        private GpioPin pin;
        private ObservableEvent<ISwitch, bool> switchedEvent;
        private bool usePullResistors = true;
        #endregion // Member Variables

        #region Constructors
        /// <summary>
        /// Initializes a new <see cref="Switch"/> instance.
        /// </summary>
        public Switch()
        {
            // Create events
            switchedEvent = new ObservableEvent<ISwitch, bool>(this);
        }
        #endregion // Constructors


        #region Internal Methods
        private void InitIO()
        {
            // If we're already initialized, ignore
            if (initialized) { return; }

            // Validate that the pin has been set
            if (pin == null) { throw new MissingIoException("Pin"); }

            // Consider ourselves initialized now
            initialized = true;

            bool driveSet = false;
            // Use pull resistors?
            if (usePullResistors)
            {
                // Check if resistors are supported 
                if (onValue == GpioPinValue.High)
                {
                    if (pin.IsDriveModeSupported(GpioPinDriveMode.InputPullDown))
                    {
                        pin.SetDriveMode(GpioPinDriveMode.InputPullDown);
                        driveSet = true;
                    }
                }
                else
                {
                    if (pin.IsDriveModeSupported(GpioPinDriveMode.InputPullUp))
                    {
                        pin.SetDriveMode(GpioPinDriveMode.InputPullUp);
                        driveSet = true;
                    }
                }
            }

            if (!driveSet)
            {
                pin.SetDriveMode(GpioPinDriveMode.Input);
            }

            // Set a debounce timeout to filter out switch bounce noise
            if (debounceTimeout > 0)
            {
                pin.DebounceTimeout = TimeSpan.FromMilliseconds(debounceTimeout);
            }

            // Determine statate
            IsOn = (pin.Read() == onValue);

            // Subscribe to pin events
            pin.ValueChanged += Pin_ValueChanged;
        }
        #endregion // Internal Methods

        #region Overrides / Event Handlers

        private void Pin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs e)
        {
            var edge = e.Edge;
            if ((onValue == GpioPinValue.High) && (edge == GpioPinEdge.RisingEdge))
            {
                IsOn = true;
            }
            else if ((onValue == GpioPinValue.Low) && (edge == GpioPinEdge.FallingEdge))
            {
                IsOn = true;
            }
            else
            {
                IsOn = false;
            }
        }

        public void Dispose()
        {
            initialized = false;
            if (pin != null)
            {
                pin.ValueChanged -= Pin_ValueChanged;
                pin.Dispose();
                pin = null;
            }
        }
        #endregion // Overrides / Event Handlers

        #region Public Properties
        /// <summary>
        /// Gets or sets the amount of time in milliseconds that will be used to debounce the switch.
        /// </summary>
        /// <value>
        /// The amount of time in milliseconds that will be used to debounce the switch. The default 
        /// is 50.
        /// </value>
        [DefaultValue(50)]
        public double DebounceTimeout
        {
            get
            {
                return debounceTimeout;
            }
            set
            {
                if (value != debounceTimeout)
                {
                    debounceTimeout = value;
                    if (pin != null)
                    {
                        pin.DebounceTimeout = TimeSpan.FromMilliseconds(debounceTimeout);
                    }
                }
            }
        }
        /// <summary>
        /// Gets a value that indicates if the switch is on.
        /// </summary>
        /// <remarks>
        /// <c>true</c> if the switch is on; otherwise false.
        /// </remarks>
        public bool IsOn
        {
            get
            {
                return isOn;
            }
            private set
            {
                // Ensure changing
                if (value == isOn) { return; }

                // Update
                isOn = value;

                // Notify
                switchedEvent.Raise(this, isOn);
            }
        }

        /// <summary>
        /// Gets or sets an optional name for the device.
        /// </summary>
        /// <value>
        /// An optional name for the device.
        /// </value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="GpioPinValue"/> that indicates the switch is on.
        /// </summary>
        /// <value>
        /// The <see cref="GpioPinValue"/> that indicates the switch is on. 
        /// The default is <see cref="GpioPinValue.High"/>.
        /// </value>
        [DefaultValue(GpioPinValue.High)]
        public GpioPinValue OnValue { get { return onValue; } set { onValue = value; } }

        /// <summary>
        /// Gets or sets the pin that the switch is connected to.
        /// </summary>
        public GpioPin Pin
        {
            get
            {
                return pin;
            }
            set
            {
                if (initialized) { throw new IoChangeException(); }
                pin = value;
            }
        }

        /// <summary>
        /// Gets or sets a value that indicates if integrated pull up or pull 
        /// down resistors should be used to help maintain the state of the pin.
        /// </summary>
        /// <value>
        /// <c>true</c> if integrated pull up or pull down resistors should; 
        /// otherwise false. The default is <c>true</c>.
        /// </value>
        [DefaultValue(true)]
        public bool UsePullResistors => usePullResistors;
        #endregion // Public Properties

        #region Public Events
        /// <summary>
        /// Occurs when the switch is switched.
        /// </summary>
        public event TypedEventHandler<ISwitch, bool> Switched
        {
            add
            {
                return switchedEvent.Add(value);
            }
            remove
            {
                switchedEvent.Remove(value);
            }
        }
        #endregion // Public Events


        #region IEventObserver Interface
        void IEventObserver.FirstHandlerAdded(object sender)
        {
            InitIO();
        }

        void IEventObserver.HandlerAdded(object sender)
        {

        }

        void IEventObserver.HandlerRemoved(object sender)
        {

        }

        void IEventObserver.LastHandlerRemoved(object sender)
        {

        }
        #endregion // IEventObserver Interface

    }
}
