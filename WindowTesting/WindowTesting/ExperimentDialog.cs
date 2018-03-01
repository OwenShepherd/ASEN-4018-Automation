﻿using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowTesting
{
    public partial class ExperimentDialog : Form
    {

        private string experimentPath;
        private string schedulePath;
        private string experimentName;
        private bool isSelected;

        public ExperimentDialog()
        {
            InitializeComponent();
            experimentPath = "Not Selected";
            ScheduleDirectory.Text = "Not Selected";
            isSelected = false;
        }

        public string getSelectedPath()
        {
            return experimentPath;
        }

        public string getExperimentName()
        {
            return experimentName;
        }

        public bool getSelected()
        {
            return isSelected;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog expLoc = new FolderBrowserDialog();

            if (expLoc.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                experimentPath = expLoc.SelectedPath;

                isSelected = true;
            }

            ExperimentDirectory.Text = experimentPath;
        }

        private void ScheduleBrowse_Click(object sender, EventArgs e)
        {
            // Opening a dialog for the user to browse for the experiment file
            Stream myStream = null;
            OpenFileDialog scheduleDialog = new OpenFileDialog();

            scheduleDialog.InitialDirectory = "c:\\";
            scheduleDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
            scheduleDialog.FilterIndex = 1;
            scheduleDialog.RestoreDirectory = true;

            if (scheduleDialog.ShowDialog() == DialogResult.OK)
            {
                schedulePath = scheduleDialog.FileName;
            }

            ScheduleDirectory.Text = schedulePath;
        }



        private void Form1_Load(object sender, EventArgs e)
        {



        }

        private void Button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Label1_Click(object sender, EventArgs e)
        {

        }

        private void ExperimentDirectory_Click(object sender, EventArgs e)
        {

        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            this.experimentName = ExpDialog.Text;
            isSelected = true;

            // Getting the path that the user had selected
            string userPath = this.getSelectedPath();
            string experimentName = this.getExperimentName();

            // Getting the experiment name
            string experimentPath = userPath + "\\" + experimentName;

            WindowTesting.ExperimentDirectory initialDirectory = new WindowTesting.ExperimentDirectory(experimentPath);
            /*
            string newState = initialDirectory.CreateNewState();
            string newState2 = initialDirectory.CreateNewState();
            */

            using (StreamReader sr = new StreamReader(schedulePath))
            {
                string currentLine;
                // currentLine will be null when the StreamReader reaches the end of file
                while ((currentLine = sr.ReadLine()) != null)
                {
                    // Search, case insensitive, if the currentLine contains the searched keyword
                    if (currentLine.IndexOf("I/RPTGEN", StringComparison.CurrentCultureIgnoreCase) >= 0)
                    {
                        Console.WriteLine(currentLine);
                    }
                }
            }
        }
    }
}