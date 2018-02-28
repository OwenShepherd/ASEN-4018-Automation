﻿namespace Thorlabs.WFS.Demo
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Thorlabs.WFS.Interop;
    

    public class Program
    {
        #region Defines
        private static int sampleCameraResolWfs = 2; // CAM_RES_768 = 768x768 pixels
        private static int sampleCameraResolWfs10 = 2; // CAM_RES_WFS10_360 = 360x360 pixels
        private static int sampleCameraResolWfs20 = 3; // CAM_RES_WFS20_512 = 512x512 pixels
        private static int pixelFormat = 0; // PIXEL_FORMAT_MONO8 = 0
        private static int sampleRefPlane = 0; // WFS_REF_INTERNAL = 0
        private static double samplePupilCentroidX = 0.0; // in mm
        private static double samplePupilCentroidY = 0.0;
        private static double samplePupilDiameterX = 2.0; // in mm, needs to fit to selected camera resolution
        private static double samplePupilDiameterY = 2.0; 
        private static int sampleImageReadings = 10; // trials to read a exposed spotfield image 
        private static int sampleOptionDynNoiseCut = 1; // use dynamic noise cut features
        private static int sampleOptionCalcSpotDias = 0; // don't calculate spot diameters
        private static int sampleOptionCancelTilt = 1; // cancel average wavefront tip and tilt
        private static int sampleOptionLimitToPupil = 0; // don't limit wavefront calculation to pupil interior
        private static int sampleZernikeOrders = 3; // calculate up to 3rd Zernike order
        private static int maxZernikeModes = 66; // allocate Zernike array of 67 because index is 1..66
        private static int sampleOptionHighspeed = 1; // use highspeed mode (only for WFS10 and WFS20 instruments)
        private static int sampleOptionHsAdaptCentr = 1; // adapt centroids in highspeed mode to previously measured centroids
        private static int sampleHsNoiseLevel = 30; // cut lower 30 digits in highspeed mode
        private static int sampleOptionHsAllowAutoexpos = 1; // allow autoexposure in highspeed mode (runs somewhat slower)
        private static int sampleWavefrontType = 0; // WAVEFRONT_MEAS = 0
        private static int samplePrintoutSpots = 5; // printout results for first 5 x 5 spots only
        private static int bufferSize = 255;
        #endregion
        
        #region Fields
        private static WFS instrument = new WFS(IntPtr.Zero);
        #endregion

        #region Main
        /// <summary>
        /// Main Methods
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            try
            {
                ConsoleKeyInfo waitKey;

                int selectedInstrId = 0;
                string resourceName = default(string);

                Console.WriteLine("==============================================================");
                Console.WriteLine(" Thorlabs instrument driver sample for WFS series instruments" );
                Console.WriteLine("==============================================================");
                Console.WriteLine("Eyyyyyyyy");

                StringBuilder wfsDriverRev = new StringBuilder(WFS.BufferSize);
                StringBuilder camDriverRevision = new StringBuilder(WFS.BufferSize);
                instrument.revision_query(wfsDriverRev, camDriverRevision);
                Console.WriteLine();
                Console.Write("WFS instrument driver version : ");
                Console.WriteLine(wfsDriverRev.ToString(), camDriverRevision.ToString());
                Console.WriteLine();

                SelectInstrument(out selectedInstrId, out resourceName);

                if (0 > selectedInstrId)
                    return;
                
                Console.Write("\nResource name of selected WFS: ");
                Console.WriteLine(resourceName.ToString());
                Console.WriteLine();

                Console.WriteLine("\n-----------------------------------------------------------\n");
                Console.WriteLine(">> Initialize Device <<");

                instrument = new WFS(resourceName, false, false);

                Console.WriteLine(">> Get Device Info <<");
                StringBuilder manufacturerName = new StringBuilder(WFS.BufferSize);
                StringBuilder instrumentName = new StringBuilder(WFS.BufferSize);
                StringBuilder serialNumberWfs = new StringBuilder(WFS.BufferSize);
                StringBuilder serialNumberCam = new StringBuilder(WFS.BufferSize);
                instrument.GetInstrumentInfo(manufacturerName, instrumentName, serialNumberWfs, serialNumberCam);
                
                Console.WriteLine(">> Opened Instrument <<");
                Console.Write("Manufacturer           : ");
                Console.WriteLine(manufacturerName);
                Console.Write("Instrument Name        : ");
                Console.WriteLine(instrumentName);
                Console.Write("Serial Number WFS      : ");
                Console.WriteLine(serialNumberWfs);
                
                // select a microlens array, WFS kits can operate 2 or more MLAs
                SelectMla();
                
                // Configure the camera resolution to a pre-defined setting
                Console.WriteLine("\n>> Configure Device, use pre-defined settings <<");
                ConfigureDevice(selectedInstrId);

                // set camera exposure time and gain if you don't want to use auto exposure
                // use functions GetExposureTimeRange, SetExposureTime, GetMasterGainRange, SetMasterGain

                // set WFS internal reference plane
                Console.WriteLine("\nSet WFS to internal reference plane.\n");
                instrument.SetReferencePlane(sampleRefPlane);

                // define pupil size and position, Zernike results are related to pupil
                DefinePupil();

                // use the autoexposure feature to find the optimal exposure time and gain
                AdjustImageBrightness();

                // the camera image can be retrieved for later display
                GetSpotfieldImage();

                // calculate and display the centroid positions of the spots
                CalcSpotCentroids();
                Console.WriteLine("\nPress <ANY_KEY> to proceed...");
                waitKey = Console.ReadKey(true);

                // calculate and display the beam parameters, derived from the centroid intensities
                CalcBeamCentroid();
                Console.WriteLine("\nPress <ANY_KEY> to proceed...");
                waitKey = Console.ReadKey(true);

	            // calculate spot deviations to internal reference positions
                CalcSpotDeviations();
                Console.WriteLine("\nPress <ANY_KEY> to proceed...");
                waitKey = Console.ReadKey(true);

                // calculate and display the wavefront
                CalcWavefront();
                Console.WriteLine("\nPress <ANY_KEY> to proceed...");
                waitKey = Console.ReadKey(true);

                // calculate and display a pre-defined number of Zernike results
                CalcZernikes();
                Console.WriteLine("\nPress <ANY_KEY> to proceed...");
                waitKey = Console.ReadKey(true);

                // enter highspeed mode and read calculated spot centroids directly from the camera
                // this enables faster measurements
                HighspeedMode(selectedInstrId);
                Console.WriteLine("\nEnd of Sample Program, press <ANY_KEY> to exit.");
                waitKey = Console.ReadKey(false);

	            // Close instrument, important to release allocated driver data!
                instrument.Dispose();
            }
            catch (System.Runtime.InteropServices.ExternalException ex)
            {
                Console.Write("Error occured >");
                Console.Write(ex.ErrorCode.ToString());
                Console.Write("< " + ex.Message);
            }
        }
        #endregion Main


        /// <summary>
        ///  select a WFS instrument from a list of connected instruments
        ///  WFS150/300, WFS10 and WFS20 models are supported
        ///  you need to type in the instrument id number
        /// </summary>
        #region Helper Methods
        private static void SelectInstrument(out int selectedInstrId, out string resourceName)
        {
            resourceName = null;
            int instrCount;
            instrument.GetInstrumentListLen(out instrCount);
            if (0 == instrCount)
            {
                Console.WriteLine("No Wavefront Sensor instrument found!");
                selectedInstrId = -1;
                ConsoleKeyInfo waitKey = Console.ReadKey(true);
                return;
            }

            Console.WriteLine("Available Wavefront Sensor instruments:\n");
            int deviceID;
            int inUse;
            
            StringBuilder instrumentName = new StringBuilder(WFS.BufferSize);
            StringBuilder instrumentSN = new StringBuilder(WFS.BufferSize);
            StringBuilder resourceNameTemp = new StringBuilder(WFS.BufferSize);
           
            for (int i = 0; i < instrCount; ++i)
            {
                instrument.GetInstrumentListInfo(i, out deviceID, out inUse, instrumentName, instrumentSN, resourceNameTemp);
                resourceName = resourceNameTemp.ToString();
                Console.Write(deviceID.ToString() + "  " + instrumentName.ToString() + "  " + instrumentSN.ToString() + ((0 == inUse) ? "" : "  (inUse)") + "\n\n");
            }

            Console.Write("Select a Wavefront Sensor instrument: ");

            string input = Console.ReadLine();
            bool chk = Int32.TryParse(input.ToString(), out selectedInstrId);
            if (!chk)
            {
                throw new Exception("Invalid selection", new Exception(input.ToString()));
            }
            
            // get selected resource name
            int deviceIDtemp = 0; 
            for (int i = 0; (i < instrCount) && (deviceIDtemp != selectedInstrId); ++i)
            {
                instrument.GetInstrumentListInfo(i, out deviceID, out inUse, instrumentName, instrumentSN, resourceNameTemp);
                deviceIDtemp = deviceID;
                resourceName = resourceNameTemp.ToString();
            }

            // selectedInstrId available?
            if (deviceIDtemp != selectedInstrId)
            {
                throw new Exception("Invalid selection", new Exception(selectedInstrId.ToString()));
            }
        }


        /// <summary>
        ///  select a microlens array, WFS kits can operate 2 or more MLAs
        ///  you need to type in the MLA index number
        /// </summary>
        private static void SelectMla()
        {
            int selectedMla;
            int mlaCount;
            instrument.GetMlaCount(out mlaCount);
 
            Console.WriteLine("\nAvailable Microlens Arrays:\n");
            StringBuilder mlaName = new StringBuilder(WFS.BufferSize);
            double camPitch;
            double lensletPitch;
            double spotOffsetX;
            double spotOffsetY;
            double lensletFum;
            double grdCorr0;
            double grdCorr45;
            for (int i = 0; i < mlaCount; ++i)
            {
                instrument.GetMlaData(i, mlaName, out camPitch, out lensletPitch, out spotOffsetX, out spotOffsetY, out lensletFum, out grdCorr0, out grdCorr45);
                Console.Write(i.ToString() + "  " + mlaName.ToString() + "  " + camPitch.ToString() + "  " + lensletPitch.ToString() + "\n");
            }

            Console.Write("\nSelect a Microlens Array: ");
            string input = Console.ReadLine();
            bool chk = Int32.TryParse(input.ToString(), out selectedMla);
            if (!chk)
            {
                throw new Exception("Invalid selection", new Exception(input.ToString()));
            }
            else
            {
                if ((0 > selectedMla) || (mlaCount <= selectedMla))
                {
                    throw new Exception("Invalid selection", new Exception(selectedMla.ToString()));
                }
                else
                {
                    instrument.SelectMla(selectedMla);
                }
            }
        }

        /// <summary>
        /// set the camera to a pre-defined resolution (pixels x pixels)
        /// this image size needs to fit the beam size and pupil size
        /// </summary>
        private static void ConfigureDevice(int selectedInstrId)
        {
            int spotsX = 0;
            int spotsY = 0;
            if ((0 == (selectedInstrId & WFS.DeviceOffsetWFS10)) &&
                (0 == (selectedInstrId & WFS.DeviceOffsetWFS20)))
            {
                Console.Write("Configure WFS camera with resolution index: " + sampleCameraResolWfs.ToString() + " (" + WFS.CamWFSXPixel[sampleCameraResolWfs].ToString() + " x " + WFS.CamWFSYPixel[sampleCameraResolWfs].ToString() + " pixels)\n");
                instrument.ConfigureCam(pixelFormat, sampleCameraResolWfs, out spotsX, out spotsY);
            }
            else
            {
                if (0 != (selectedInstrId & WFS.DeviceOffsetWFS10)) // WFS10 instrument
                {
                    Console.Write("Configure WFS10 camera with resolution index: " + sampleCameraResolWfs10.ToString() + " (" + WFS.CamWFSXPixel[sampleCameraResolWfs10].ToString() + " x " + WFS.CamWFSYPixel[sampleCameraResolWfs10].ToString() + " pixels)\n");
                    instrument.ConfigureCam(pixelFormat, sampleCameraResolWfs10, out spotsX, out spotsY);
                }
                else // WFS20 instrument
                {
                    Console.Write("Configure WFS20 camera with resolution index: " + sampleCameraResolWfs20.ToString() + " (" + WFS.CamWFSXPixel[sampleCameraResolWfs20].ToString() + " x " + WFS.CamWFSYPixel[sampleCameraResolWfs20].ToString() + " pixels)\n");
                    instrument.ConfigureCam(pixelFormat, sampleCameraResolWfs20, out spotsX, out spotsY);
                }
            }
            Console.WriteLine("Camera is configured to detect " + spotsX.ToString() + " x " + spotsY.ToString() + " lenslet spots.\n", spotsX, spotsY);
        }

        /// <summary>
        /// Set the pupil to pre-defined values, the pupil needs to fit the selected camera resolution and the beam diameter
        /// Zernike results depend and relate to the pupil size!
        /// </summary>
        private static void DefinePupil()
        {
            Console.WriteLine("\nDefine pupil to:");
            Console.WriteLine("Centroid_x = " + samplePupilCentroidX.ToString("F3"));
            Console.WriteLine("Centroid_y = " + samplePupilCentroidY.ToString("F3"));
            Console.WriteLine("Diameter_x = " + samplePupilDiameterX.ToString("F3"));
            Console.WriteLine("Diameter_y = " + samplePupilDiameterY.ToString("F3") + "\n");
            instrument.SetPupil(samplePupilCentroidX, samplePupilCentroidY, samplePupilDiameterX, samplePupilDiameterY);
        }


        /// <summary>
        /// use the autoexposure feature to get a well exposed camera image
        /// repeate image reading in case of badly saturated image
        /// suited exposure time and gain settings are adjusted within the function TakeSpotfieldImageAutoExpos()
        /// </summary>
        private static void AdjustImageBrightness()
        {
            ConsoleKeyInfo waitKey;
            int status;
            int expos_ok = 0;
            double exposAct;
            double masterGainAct;

            Console.WriteLine("\nRead camera images:\n");
            Console.WriteLine("Image No.     Status     ->   newExposure[ms]   newGainFactor");

            for (int cnt = 0; cnt < sampleImageReadings; ++cnt)
            {
                // take a camera image with auto exposure, note that there may several function calls required to get an optimal exposed image
                instrument.TakeSpotfieldImageAutoExpos(out exposAct, out masterGainAct);
                Console.Write("    " + cnt.ToString() + "     ");

                // check instrument status for non-optimal image exposure
                instrument.GetStatus(out status);

                if (0 != (status & WFS.StatBitHighPower))
                {
                    Console.Write("Power too high!    ");
                }
                else if (0 != (status & WFS.StatBitLowPower))
                {
                    Console.Write("Power too low!     ");
                }
                else if (0 != (status & WFS.StatBitHighAmbientLight))
                {
                    Console.Write("High ambient light!");
                }
                else
                {
                    Console.Write("OK                 ");
                }
                Console.WriteLine("     " + exposAct.ToString("F3") + "          " + masterGainAct.ToString("F3"));

                if ((0 == (status & WFS.StatBitHighPower)) &&
                    (0 == (status & WFS.StatBitLowPower)) &&
                    (0 == (status & WFS.StatBitHighAmbientLight)))
                {
                    expos_ok = 1;
                    break; // image well exposed and is usable
                }
            }

            // close program if no well exposed image is feasible
            if (0 == expos_ok)
            {
                Console.Write("\nSample program will be closed because of unusable image quality, press <ANY_KEY>.");
                instrument.Dispose(); // required to release allocated driver data
                waitKey = Console.ReadKey(true);
                throw new Exception("Unusable Image");
            }
        }


        /// <summary>
        /// get last camera image called 'spotfield'
        /// this is only required for later display
        /// </summary>
        private static void GetSpotfieldImage()
        {
            byte[] imageBuffer = new byte[WFS.ImageBufferSize];
            int rows;
            int cols;
            instrument.GetSpotfieldImage(imageBuffer, out rows, out cols);
        }


        /// <summary>
        /// calculate all spot centroid positions using dynamic noise cut option
        /// </summary>
        private static void CalcSpotCentroids()
        {
            instrument.CalcSpotsCentrDiaIntens(sampleOptionDynNoiseCut, sampleOptionCalcSpotDias);

            // get centroid result arrays
            float[,] centroidX = new float[WFS.MaxSpotY, WFS.MaxSpotX];
            float[,] centroidY = new float[WFS.MaxSpotY, WFS.MaxSpotX];
            instrument.GetSpotCentroids(centroidX, centroidY);

            // print out some centroid positions
            Console.WriteLine("\nCentroid X Positions in pixels (first 5x5 elements)\n");
            for (int i = 0; i < samplePrintoutSpots; ++i)
            {
                for (int j = 0; j < samplePrintoutSpots; ++j)
                {
                    Console.Write(" " + centroidX[i, j].ToString("F3"));
                }
                Console.WriteLine("");
            }

            Console.WriteLine("\nCentroid Y Positions in pixels (first 5x5 elements)\n");
            for (int i = 0; i < samplePrintoutSpots; ++i)
            {
                for (int j = 0; j < samplePrintoutSpots; ++j)
                {
                    Console.Write("  " + centroidY[i, j].ToString("F3"));
                }
                Console.WriteLine("");
            }
        }


        /// <summary>
        /// calculate centroid and diameter of the optical beam
        /// you may use this beam data to define a pupil variable in position and size
        /// for WFS20: calculation is based on centroid intensties calculated by WFS_CalcSpotsCentrDiaIntens()
        /// </summary>
        private static void CalcBeamCentroid()
        {
            double beamCentroidX, beamCentroidY, beamDiameterX, beamDiameterY;
            instrument.CalcBeamCentroidDia(out beamCentroidX, out beamCentroidY, out beamDiameterX, out beamDiameterY);
            	
            Console.WriteLine("\nInput beam is measured to:");
            Console.WriteLine("CentroidX = " + beamCentroidX.ToString("F3") + " mm");
            Console.WriteLine("CentroidY = " + beamCentroidY.ToString("F3") + " mm");
            Console.WriteLine("DiameterX = " + beamDiameterX.ToString("F3") + " mm");
            Console.WriteLine("DiameterY = " + beamDiameterY.ToString("F3") + " mm");
        }
        
        
        /// <summary>
        /// calculate and printout spot deviations to reference positions
        /// </summary>
        private static void CalcSpotDeviations()
        {
            instrument.CalcSpotToReferenceDeviations(sampleOptionCancelTilt);

            // get spot deviations
            float[,] deviationX = new float[WFS.MaxSpotY, WFS.MaxSpotX];
            float[,] deviationY = new float[WFS.MaxSpotY, WFS.MaxSpotX];
            instrument.GetSpotDeviations(deviationX, deviationY);

            // print out some spot deviations
            Console.WriteLine("\nSpot Deviation X in pixels (first 5x5 elements)\n");
            for (int i = 0; i < samplePrintoutSpots; ++i)
            {
                for (int j = 0; j < samplePrintoutSpots; ++j)
                {
                    Console.Write(" " + deviationX[i, j].ToString("F3"));
                }
                Console.WriteLine("");
            }
            Console.WriteLine("\nSpot Deviation Y in pixels (first 5x5 elements)\n");
            for (int i = 0; i < samplePrintoutSpots; ++i)
            {
                for (int j = 0; j < samplePrintoutSpots; ++j)
                {
                    Console.Write(" " + deviationY[i, j].ToString("F3"));
                }
                Console.WriteLine("");
            }
        }


        /// <summary>
        /// calculate and printout measured wavefront
        /// </summary>
        private static void CalcWavefront()
        {
            ConsoleKeyInfo waitKey;
            float[,] wavefront = new float[WFS.MaxSpotY, WFS.MaxSpotX];
            instrument.CalcWavefront(sampleWavefrontType, sampleOptionLimitToPupil, wavefront);

            // print out some wavefront points
            Console.WriteLine("\nWavefront in microns (first 5x5 elements)\n");
            for (int i = 0; i < samplePrintoutSpots; ++i)
            {
                for (int j = 0; j < samplePrintoutSpots; ++j)
                {
                    Console.Write(" " + wavefront[i, j].ToString("F3"));
                }
                Console.WriteLine("");
            }
            Console.WriteLine("\nPress <ANY_KEY> to proceed...");
            waitKey = Console.ReadKey(true);

            // calculate wavefront statistics within defined pupil
            double wavefrontMin, wavefrontMax, wavefrontDiff, wavefrontMean, wavefrontRms, wavefrontWeightedRms;
            instrument.CalcWavefrontStatistics(out wavefrontMin, out wavefrontMax, out wavefrontDiff, out wavefrontMean, out wavefrontRms, out wavefrontWeightedRms);
            Console.WriteLine("\nWavefront Statistics in microns:");
            Console.WriteLine("Min          : " + wavefrontMin.ToString("F3"));
            Console.WriteLine("Max          : " + wavefrontMax.ToString("F3"));
            Console.WriteLine("Diff         : " + wavefrontDiff.ToString("F3"));
            Console.WriteLine("Mean         : " + wavefrontMean.ToString("F3"));
            Console.WriteLine("RMS          : " + wavefrontRms.ToString("F3"));
            Console.WriteLine("Weigthed RMS : " + wavefrontWeightedRms.ToString("F3"));
        }


        /// <summary>
        /// calculate Zernike results in microns, related to defined pupil size
        /// </summary>
        private static void CalcZernikes()
        {
            Console.WriteLine("\nZernike fit up to order " + sampleZernikeOrders.ToString());
            int zernike_order = sampleZernikeOrders;
            float[] zernikeUm = new float[maxZernikeModes + 1];
            float[] zernikeOrdersRmsUm = new float[maxZernikeModes + 1];
            double rocMm;
            instrument.ZernikeLsf(out zernike_order, zernikeUm, zernikeOrdersRmsUm, out rocMm); // also calculates deviation from centroid data for wavefront integration

            Console.WriteLine("\nZernike Mode    Coefficient");
            for (int i = 0; i < WFS.ZernikeModes[sampleZernikeOrders]; ++i)
            {
                Console.WriteLine("  " + i.ToString() + "             " + zernikeUm[i].ToString("F3"));
            }
        }


        /// <summary>
        /// enter highspeed mode for WFS10 and WFS20 instruments only
        /// in this mode the camera itself calculates the spot centroids
        /// this enables much faster measurements
        /// </summary>
        private static void HighspeedMode(int selectedInstrId)
        {
            ConsoleKeyInfo waitKey;
            if ((0 != (selectedInstrId & WFS.DeviceOffsetWFS10)) ||
                (0 != (selectedInstrId & WFS.DeviceOffsetWFS20))) // for WFS10 or WFS20 instrument only
            {
                Console.Write("\nEnter Highspeed Mode 0/1? ");
                string input = Console.ReadLine();
                int selection;
                bool chk = Int32.TryParse(input.ToString(), out selection);
                if (!chk)
                {
                    throw new Exception("Invalid selection", new Exception(input.ToString()));
                }
                else
                {
                    if ((0 != selection) && (1 != selection))
                    {
                        throw new Exception("Invalid selection", new Exception(selection.ToString()));
                    }
                }

                if (1 == selection)
                {
                    // set highspeed mode active, use pre-defined options, refere to WFS_SetHighspeedMode() function help
                    instrument.SetHighspeedMode(sampleOptionHighspeed, sampleOptionHsAdaptCentr, sampleHsNoiseLevel, sampleOptionHsAllowAutoexpos);
                    int hsWinCountX, hsWinCountY, hsWinSizeX, hsWinSizeY;
                    int[] hsWinStartX = new int[WFS.MaxSpotX];
                    int[] hsWinStartY = new int[WFS.MaxSpotY];

                    instrument.GetHighspeedWindows(out hsWinCountX, out hsWinCountY, out hsWinSizeX, out hsWinSizeY, hsWinStartX, hsWinStartY);

                    Console.WriteLine("\nCentroid detection windows are defined as follows:\n"); // refere to WFS_GetHighspeedWindows function help
                    Console.WriteLine("CountX = " + hsWinCountX.ToString() + ", CountY = " + hsWinCountY.ToString());
                    Console.WriteLine("SizeX  = " + hsWinSizeX.ToString() + ", SizeY  = " + hsWinSizeY.ToString());
                    Console.WriteLine("Start coordinates x: ");
                    for (int i = 0; i < hsWinCountX; ++i)
                    {
                        Console.Write(hsWinStartX[i].ToString() + " ");
                    }
                    Console.WriteLine("\n");
                    Console.WriteLine("Start coordinates y: ");
                    for (int i = 0; i < hsWinCountY; ++i)
                    {
                        Console.Write(hsWinStartY[i].ToString() + " ");
                    }
                    Console.WriteLine("\n");

                    Console.WriteLine("\nPress <ANY_KEY> to proceed...");
                    waitKey = Console.ReadKey(false);

                    double exposAct;
                    double masterGainAct;
                    // take a camera image with auto exposure, this is also supported in highspeed-mode
                    instrument.TakeSpotfieldImageAutoExpos(out exposAct, out masterGainAct);
                    Console.WriteLine("\nexposure = " + exposAct.ToString("F3") + " ms, gain =  " + masterGainAct.ToString("F3") + "\n");

                    double beamCentroidX;
                    double beamCentroidY;
                    double beamDiameterX;
                    double beamDiameterY;
                    // get centroid and diameter of the optical beam, these data are based on the detected centroids
                    instrument.CalcBeamCentroidDia(out beamCentroidX, out beamCentroidY, out beamDiameterX, out beamDiameterY);
                    Console.WriteLine("\nInput beam is measured to:\n");
                    Console.WriteLine("CentroidX = " + beamCentroidX.ToString("F3") + " mm");
                    Console.WriteLine("CentroidY = " + beamCentroidY.ToString("F3") + " mm");
                    Console.WriteLine("DiameterX = " + beamDiameterX.ToString("F3") + " mm");
                    Console.WriteLine("DiameterY = " + beamDiameterY.ToString("F3") + " mm");

                    Console.WriteLine("\nPress <ANY_KEY> to proceed...");
                    waitKey = Console.ReadKey(false);

                    // Info: calling WFS_CalcSpotsCentrDiaIntens() is not required because the WFS10/WFS20 camera itself already did the calculation

                    float[,] centroidX = new float[WFS.MaxSpotY, WFS.MaxSpotX];
                    float[,] centroidY = new float[WFS.MaxSpotY, WFS.MaxSpotX];
                    // get centroid result arrays
                    instrument.GetSpotCentroids(centroidX, centroidY);

                    // print out some centroid positions
                    Console.WriteLine("\nCentroid X Positions in pixels (first 5x5 elements)\n");
                    for (int i = 0; i < samplePrintoutSpots; ++i)
                    {
                        for (int j = 0; j < samplePrintoutSpots; ++j)
                        {
                            Console.Write(" " + centroidX[i, j].ToString("F3"));
                        }
                        Console.WriteLine("");
                    }

                    Console.WriteLine("\nCentroid Y Positions in pixels (first 5x5 elements)\n");
                    for (int i = 0; i < samplePrintoutSpots; ++i)
                    {
                        for (int j = 0; j < samplePrintoutSpots; ++j)
                        {
                            Console.Write(" " + centroidY[i, j].ToString("F3"));
                        }
                        Console.WriteLine("");
                    }

                    Console.WriteLine("\nThe following wavefront and Zernike calculations can be done identical to normal mode.\n");
                }
            }
        }
        #endregion Helper Methods
    }
}