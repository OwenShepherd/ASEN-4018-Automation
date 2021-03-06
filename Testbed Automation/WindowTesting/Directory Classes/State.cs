﻿using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;



namespace ASEN
{
    class State
    {
        public double RCWS_EXPT; // Exposure time for the RCWS [microseconds]
        public double SHA_EXPT; // Exposure time for the SHA [microseconds]
        public double RCWS_DFORE; // RCWS Foreward defocus distance [micro-meters] 
        public double RCWS_DAFT; // RCWS Aftward defocus distance [micro-meters]
        public double MA_X; // Mirror 2 x-displacement [arc-seconds]
        public double MA_Y; // Mirror 2 y-displacement [arc-seconds]
        public string path; // Path of the state root folder
        public string cameraInUse;
        private string rootPath;
        private string[] serials;
        private ASEN_SHA currentSHA; // The object that controls interactions with the SHA
        private ASEN_ENV teensy; // The object that controls interactions with the teensy
        private ASEN_RCWS currentRCWS;
        private ASEN_MotorControl motor1;
        private ASEN_MotorControl motor2;
        private ASEN_MotorControl motor3;
        private object teensyLock;
        private bool READ;
        public int velocity;
        private string teensyPort;
        private string ipyPath;
        private string pyPath;
        private string RCWSPath;
        private string SHAPath;
        private bool ISASI;
        private ProcessStartInfo Py;


        public State(double[] parameters, bool selectedCamera, string statePath, string[] serials, string COMPort, string pythonPath, string ipythonPath)
        {
            // Collecting the state parameters from the input array
            RCWS_EXPT = parameters[0];
            SHA_EXPT = parameters[1];
            RCWS_DFORE = parameters[2];
            RCWS_DAFT = parameters[3];
            MA_X = parameters[4];
            MA_Y = parameters[5];
            //cameraInUse = selectedCamera;
            ISASI = selectedCamera;
            this.serials = serials;
            this.velocity = 3200; // Velocity units are unknown / stupid...
            this.path = statePath;
            this.teensyLock = new object();
            this.teensyPort = COMPort;
            this.ipyPath = ipythonPath;
            this.pyPath = pythonPath;

            this.RCWSPath = this.path + "\\data_RCWS";
            this.SHAPath = this.path + "\\data_SHA";

            DirectoryInfo rcws = Directory.CreateDirectory(RCWSPath);
            DirectoryInfo sha = Directory.CreateDirectory(SHAPath);
        }

        public void RunState()
        {
            // Want to create the filenames sans-extensions
            string RCWSForePath = this.RCWSPath + "\\img_RCWS_fore";
            string RCWSAftPath = this.RCWSPath + "\\img_RCWS_aft";
            /*
            // Motor Control Setup
            this.motor1 = new ASEN_MotorControl(serials[0], this.velocity);
            this.motor2 = new ASEN_MotorControl(serials[1], this.velocity);
            this.motor3 = new ASEN_MotorControl(serials[2], this.velocity);

            // Initializing the motors
            motor1.InitializeMotor();
            motor2.InitializeMotor();
            motor3.InitializeMotor();

            // Initializing the process info for the python process to run for the Env Sensors
            Py = new ProcessStartInfo(); // Initializing the process start info
            Py.FileName = pyPath;  // Path to the python exe
            Py.Arguments = "SerialReader.py" + " " + this.path + " " + this.teensyPort;  // The .py script to run and its arguments
            Py.UseShellExecute = false;// Do not use OS shell
            Py.CreateNoWindow = true; // We don't need new window
            Py.RedirectStandardOutput = true; // Redirecting output from the python stdout
            Py.RedirectStandardInput = true;  // Redirecting input from the python stdin
            Py.RedirectStandardOutput = true;// Any output, generated by application will be redirected back
            Py.RedirectStandardError = true; // Any error in standard output will be redirected back (for example exceptions)

            // Initial homing of the motors
            motor1.HomeMotor();
            motor2.HomeMotor();
            motor3.HomeMotor();

            // Now we move the motors to their initial position.
            // This means the tilt-tip stage is moved to its proper position, while the linear
            // stage is moved to the RCWS's specified fore defocus position
            motor1.MoveMotorLinear(RCWS_DFORE);
            motor2.MoveMotorPitch(MA_X);
            motor3.MoveMotorYaw(MA_Y);
            
            // ASEN_SHA Initializing Device
            currentSHA = new ASEN_SHA();
            currentSHA.CameraConnectionAndSetup();

            // Running the correct camera process depending on which camera was selected in the GUI
            if (ISASI) { ASICamera(RCWSForePath, RCWSAftPath); }
            else { QHYCamera(RCWSForePath, RCWSAftPath); }

            
            currentSHA.CloseCamera();
            motor1.DisconnectMotor();
            motor2.DisconnectMotor();
            motor3.DisconnectMotor();
            */
            ASEN_RCWS camera = new ASEN_RCWS("ASCOM.QHYCCD.Camera");
            camera.InitializeCamera();
            camera.Capture(0.05, true);
            camera.SaveImage(@"C:\Users\sheph\Desktop\QHYASOMTestFile.csv");
            camera.Disconnect();


        }

        // Process with methods to be called when using the ASI camera
        private void ASICamera(string foreFile, string aftFile)
        {
            // Name of the ASCOM driver for the ASI
            cameraInUse = "ASCOM.ASICamera2.Camera";
            ASEN_RCWS currCamera = new ASEN_RCWS(cameraInUse);
            currCamera.InitializeCamera();

            // Starting the environmental sensors process
            var envProcess = Process.Start(this.Py);

            // Take a capture of the image at the fore distance
            currCamera.Capture(RCWS_EXPT, true);
            currCamera.SaveImage(foreFile + ".csv");

            // Taking a caputre with the SHA at the fore distance
            byte[] spotFieldFore = currentSHA.GatherCameraData(SHA_EXPT, SHAPath + "\\img_fore.csv");
            float[] zernikesFore = currentSHA.ProcessCameraData(SHAPath + "\\wft_fore.csv", SHAPath + "\\spt_fore", SHAPath + "\\zernikes_fore.csv");

            // Moving the RCWS motor to the aft location
            motor1.MoveMotorLinear(RCWS_DAFT);

            // Taking a capture of the image at the aft distance
            currCamera.Capture(RCWS_EXPT, true);
            currCamera.SaveImage(aftFile + ".csv");

            // Taking a capture with the SHA at the aft distance
            byte[] spotFieldAft = currentSHA.GatherCameraData(SHA_EXPT, SHAPath + "\\img_aft.csv");
            float[] zernikesAft = currentSHA.ProcessCameraData(SHAPath + "\\wft_aft.csv", SHAPath + "\\spt_aft", SHAPath + "\\zernikes_aft.csv");


            // Ending the env. sensors process
            envProcess.StandardInput.Close();

            // Disconnecting from the ASI
            currCamera.Disconnect();
        }
        /*
        // Process with methods to be called when using the QHY camera
        private void QHYCamera(string foreFile, string aftFile)
        {
            // Initializing QHY object
            WindowTesting.Device_Classes.ASEN_QHY currCamera = new WindowTesting.Device_Classes.ASEN_QHY();
            currCamera.Initialize();

            // Starting the environmental sensors process
            var envProcess = Process.Start(this.Py);

            // Taking a capture with the QHY and saving the image file at the fore distance
            currCamera.Capture((int)RCWS_EXPT, foreFile);

            // Taking a caputre with the SHA at the fore distance
            byte[] spotFieldFore = currentSHA.GatherCameraData(SHA_EXPT, SHAPath + "\\img_fore.csv");
            float[] zernikesFore = currentSHA.ProcessCameraData(SHAPath + "\\wft_fore.csv", SHAPath + "\\spt_fore", SHAPath + "\\zernikes_fore.csv");

            // Moving the motors to the aft distance
            motor1.MoveMotorLinear(RCWS_DAFT);

            // Taking a QHY capture at the aft distance
            currCamera.Capture((int)RCWS_EXPT, aftFile);

            // Taking a capture with the SHA at the aft distance
            byte[] spotFieldAft = currentSHA.GatherCameraData(SHA_EXPT, SHAPath + "\\img_aft.csv");
            float[] zernikesAft = currentSHA.ProcessCameraData(SHAPath + "\\wft_aft.csv", SHAPath + "\\spt_aft", SHAPath + "\\zernikes_aft.csv");

            // Ending the env. sensors process, which saves the file with its readings to this point
            // Works by sending a keybaord interrupt to the python script (ctrl+c)
            envProcess.StandardInput.Close();

            // Disconnecting from the QHY
            currCamera.Close();

        }
        */
        // ------------------ Functions for Parallel Threads -----------------------------
        /*
        private void TeensyParallel(ref object teensyLock)
        {

            int dataCount = 0;
            bool localREAD;

            // Storing a local variable so that we only have to lock during the updating of the bool
            lock (teensyLock)
            {
                localREAD = this.READ;
            }

            while (localREAD)
            {

                teensy.BeginTeensyRead(ref dataCount);

                // Locking to see if the image read has completed
                lock (teensyLock)
                {
                    localREAD = this.READ;
                }

            }
        }

        private void ImagingParallel()
        {

            // Here we save the image from the RCWS
            int[,] RCWSImageFORE = new int[currentRCWS.width, currentRCWS.height];

            currentRCWS.Capture(RCWS_EXPT, false);
            // currentRCWS.saveImage(); // Not implemented yet

            // Here we save the image from the SHA
            //byte[] byteData = currentSHA.GatherCameraData(SHA_EXPT);
            //float[] zerinkes = currentSHA.ProcessCameraData();



            // Moving the RCWS to the aft defocus distance
            motor1.MoveMotor(RCWS_DAFT);

            // Taking the image again
            int[,] RCWSImageAFT = new int[currentRCWS.width, currentRCWS.height];

            currentRCWS.Capture(RCWS_EXPT, false);
            // currentRCWS.saveImage();

            // Here we save the image from the SHA
            //byteData = currentSHA.GatherCameraData(SHA_EXPT);
            //zerinkes = currentSHA.ProcessCameraData();

        }
        */


    }
}
