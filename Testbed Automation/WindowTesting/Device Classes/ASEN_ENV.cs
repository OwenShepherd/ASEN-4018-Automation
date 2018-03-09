﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Ports;

namespace ASEN
{
    class ASEN_ENV
    {
        private bool READ;
        private FileStream saveFile;
        private SerialPort teensy;
        private byte[] readData;
        public string path;

        // Let's think of some things that we want...
        // Should be a relatively simple class
        public ASEN_ENV(string COMport, string directory)
        {
            int baud = 2000000; // We can change this to anything.  Check out error rates and such for the Teensy 3.6 here: https://www.pjrc.com/teensy/td_uart.html
            this.teensy = new SerialPort(COMport,baud);

            // Appned the correct env csv path to the directory
            this.path = directory + "\\env_data.csv";

            // Open the file and set the handle
            this.saveFile = File.Open(path, FileMode.Open);

            // Write buffer
            
        }

        // I need to set read separately from everything else for parallelization
        public void PrepRead()
        {
            this.READ = true;
        }

        public void EndRead()
        {
            this.READ = false;
        }

        
        public void BeginTeensyRead()
        {
            int dataCount = 0;

            while (READ)
            {
                readData[dataCount] = (byte)teensy.ReadByte();
                dataCount++;
            }

            File.WriteAllBytes(path, readData);
        }
    }
}