﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Sales
{
    static class Program
    {

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {

            // This creates a form that prompts the user to select a directory
            ASEN.ExperimentDialog formTest = new ASEN.ExperimentDialog();
            Application.Run(formTest);
        }
    }
}
