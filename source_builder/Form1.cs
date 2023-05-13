using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices.ComTypes;


namespace UDP_Patcher
{
    public partial class Form1 : Form
    {
        public string gdiFileUnpatched;
        public string gdiFilePatched;
        public string patchFilename;
        private static List<string> patchedGDIFiles = new List<string>();

        public Form1()
        {
            InitializeComponent();
        }

        private void ButtonQuit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.CenterToScreen();
            dropdownPatchedIPBINSource.SelectedIndex = 0;

            // Perform initial sanity check for helper tools.
            string missingFiles = String.Empty;

            if(!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\tools\\xdelta.exe"))
            {
                missingFiles = missingFiles + "\n - xdelta.exe";
            }

            if(!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\tools\\gditools.exe"))
            {
                missingFiles = missingFiles + "\n - gditools.exe";
            }

            if(!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\tools\\python27.dll"))
            {
                missingFiles = missingFiles + "\n - python27.dll";
            }

            if(!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\tools\\extract.exe"))
            {
                missingFiles = missingFiles + "\n - extract.exe";
            }

            if(!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\tools\\bin2iso.exe"))
            {
                missingFiles = missingFiles + "\n - bin2iso.exe";
            }

            if(!String.IsNullOrEmpty(missingFiles))
            {
                MessageBox.Show("One or more required files is missing from the \"tools\" folder:" + missingFiles, "Universal Dreamcast Patch Builder", MessageBoxButtons.OK, MessageBoxIcon.Error);

                Application.Exit();
            }
        }

        private void ButtonSelectUnpatchedGDI_Click(object sender, EventArgs e)
        {
            OpenFileDialog openUnpatchedGDI = new OpenFileDialog();
            openUnpatchedGDI.Filter = "GDI files (*.gdi)|*.gdi";
            DialogResult resultOpenUnpatchedGDI = openUnpatchedGDI.ShowDialog();

            if(resultOpenUnpatchedGDI == DialogResult.OK)
            {
                gdiFileUnpatched = openUnpatchedGDI.FileName;

                buttonSelectPatchedGDI.Enabled = true;
            }
        }

        private void ButtonSelectPatchedGDI_Click(object sender, EventArgs e)
        {
            OpenFileDialog openPatchedGDI = new OpenFileDialog();
            openPatchedGDI.Filter = "GDI files (*.gdi)|*.gdi";
            DialogResult resultOpenPatchedGDI = openPatchedGDI.ShowDialog();

            if(resultOpenPatchedGDI == DialogResult.OK)
            {
                gdiFilePatched = openPatchedGDI.FileName;

                groupboxStep2.Enabled = true;
                groupboxStep3.Enabled = true;
                buttonBuildPatch.Enabled = true;
            }
        }

        private void ButtonBuildPatch_Click(object sender, EventArgs e)
        {
            // Disable buttons, textboxes, and groups.
            buttonQuit.Enabled = false;
            groupboxStep1.Enabled = false;
            groupboxStep2.Enabled = false;
            groupboxStep3.Enabled = false;

            // Remove any trailing or leading whitespace from patch filename and custom game name.
            textboxPatchFilename.Text = Regex.Replace(textboxPatchFilename.Text, @"^\s+|\s+$|\s+(?=\s)", "");
            textboxGameNameIPBIN.Text = Regex.Replace(textboxGameNameIPBIN.Text, @"^\s+|\s+$|\s+(?=\s)", "");

            // Patch filename is empty, do not proceed.
            if(textboxPatchFilename.Text.Length < 1)
            {
                MessageBox.Show("The patch filename cannot be empty!", "Universal Dreamcast Patch Builder", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Re-enable buttons, textboxes, and groups.
                groupboxStep1.Enabled = true;
                groupboxStep2.Enabled = true;
                groupboxStep3.Enabled = true;
                buttonQuit.Enabled = true;

                // Stop function's execution.
                return;
            }
            // Custom game name has been enabled but is empty, do not proceed.
            else if(textboxGameNameIPBIN.Text.Length < 1 && checkboxCustomNameIPBIN.Checked == true)
            {
                MessageBox.Show("The custom game name for IP.BIN cannot be empty!", "Universal Dreamcast Patch Builder", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Re-enable buttons, textboxes, and groups.
                groupboxStep1.Enabled = true;
                groupboxStep2.Enabled = true;
                groupboxStep3.Enabled = true;
                buttonQuit.Enabled = true;

                // Stop function's execution.
                return;
            }

            // Remove file extension from patch filename if it was erroneously entered.
            if(textboxPatchFilename.Text.ToLower().EndsWith(".dcp"))
            {
                patchFilename = Path.GetFileNameWithoutExtension(textboxPatchFilename.Text) + ".dcp";
                textboxPatchFilename.Text = Path.GetFileNameWithoutExtension(textboxPatchFilename.Text);
            }
            else
            {
                patchFilename = textboxPatchFilename.Text + ".dcp";
            }

            // Patch filename contains invalid characters, do not proceed.
            if(!IsValidFilename(patchFilename))
            {
                MessageBox.Show("The filename \"" + patchFilename + "\" contains invalid characters!  Please enter a new one.", "Universal Dreamcast Patch Builder", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Re-enable buttons, textboxes, and groups.
                groupboxStep1.Enabled = true;
                groupboxStep2.Enabled = true;
                groupboxStep3.Enabled = true;
                buttonQuit.Enabled = true;

                // Stop function's execution.
                return;
            }

            // If user selected to use custom IP.BIN from unpatched GDI with no additional options, show warning.
            if(checkboxUsePatchedIPBIN.Checked == true && dropdownPatchedIPBINSource.SelectedIndex == 1 && checkboxRegionFreeIPBIN.Checked == false && checkboxVGAIPBIN.Checked == false && checkboxCustomNameIPBIN.Checked == false)
            {
                DialogResult confirmIPBINPatch = MessageBox.Show("You have selected to use a custom IP.BIN using the unpatched GDI as source.  However, you haven't selected any other patching options for the IP.BIN file.\n\nAs a result, no custom IP.BIN will be included in this patch, as it would introduce no changes to that of the original retail GDI.\n\nDo you want to proceed with your current settings?", "Universal Dreamcast Patch Builder", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                // User does not want to proceed as is.
                if(confirmIPBINPatch == DialogResult.No)
                {
                    // Re-enable buttons, textboxes, and groups.
                    groupboxStep1.Enabled = true;
                    groupboxStep2.Enabled = true;
                    groupboxStep3.Enabled = true;
                    buttonQuit.Enabled = true;

                    // Stop function's execution.
                    return;
                }
                // Proceed, disabling checkbox for custom IP.BIN.
                else
                {
                    checkboxUsePatchedIPBIN.Checked = false;
                }
            }
            
            // Prompt for confirmation to proceed with patching.
            DialogResult confirmPatch = MessageBox.Show("This process wil not modify the selected unpatched GDI nor the selected patched GDI in any way.  Both GDIs will be scanned to automatically build a DCP patch file containing deltas for modified files, as well as the full version of any brand-new files detected.\n\nThe final patch file will be generated in this application's working directory with the following filename:\n\n" + patchFilename + "\n\n" + "Are you ready to proceed?", "Universal Dreamcast Patch Builder", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            // Confirm user wants to proceed.
            if(confirmPatch == DialogResult.No)
            {
                // Re-enable buttons, textboxes, and groups.
                buttonQuit.Enabled = true;
                groupboxStep1.Enabled = true;
                groupboxStep2.Enabled = true;
                groupboxStep3.Enabled = true;

                // Stop function's execution.
                return;
            }

            // Hide buttons.
            buttonBuildPatch.Visible = false;
            buttonQuit.Visible = false;

            // Show progress bar and progress details.
            patchBuildProgressBar.Visible = true;
            patchBuildProgressDetails.Visible = true;
            patchBuildProgressPercentage.Visible = true;

            // Show wait cursor.
            Cursor = Cursors.WaitCursor;

            // Change logo to show power LED on with a cool flashing effect.
            wait(250);
            pictureBox1.Image = pictureBox2.Image;
            wait(250);
            pictureBox1.Image = pictureBox3.Image;
            wait(150);
            pictureBox1.Image = pictureBox2.Image;
            wait(150);
            pictureBox1.Image = pictureBox3.Image;
            wait(150);
            pictureBox1.Image = pictureBox2.Image;

            // Sleep for half a second.
            wait(500);

            // Store filenames and paths.
            string appBaseFolder = AppDomain.CurrentDomain.BaseDirectory;
            string folderGUID = Guid.NewGuid().ToString();
            string appTempFolder = Path.GetTempPath() + "_UDP_" + folderGUID;
            string gdiBaseFolderUnpatched = String.Empty;
            string gdiBaseFolderPatched = String.Empty;

            // Create temporary folders for extracted unpatched/patched GDIs and patch files.
            try
            {
                Directory.CreateDirectory(appTempFolder + "_extracted_unpatched");
                Directory.CreateDirectory(appTempFolder + "_gdi_unpatched");
                Directory.CreateDirectory(appTempFolder + "_extracted_patched");
                Directory.CreateDirectory(appTempFolder + "_gdi_patched");
                Directory.CreateDirectory(appTempFolder + "_patch");
            }
            // Otherwise, display an error if folder creation fails.
            catch
            {
                // Display error message.
                MessageBox.Show("Unable to create necessary temporary folders!\n\nTry running Universal Dreamcast Patch Builder as Administrator.", "Universal Dreamcast Patch Builder", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Hide progress bar and reset it.
                patchBuildProgressBar.Value = 0;
                patchBuildProgressBar.Visible = false;
                patchBuildProgressDetails.Visible = false;
                patchBuildProgressPercentage.Visible = false;

                // Show previously hidden buttons.
                buttonBuildPatch.Visible = true;
                buttonQuit.Visible = true;

                // Re-enable step 1 group.
                groupboxStep1.Enabled = true;
                buttonSelectPatchedGDI.Enabled = false;

                // Change enabled/disabled status of buttons.
                buttonQuit.Enabled = true;
                buttonBuildPatch.Enabled = false;

                // Return to normal logo.
                pictureBox1.Image = pictureBox3.Image;

                // Return to normal cursor.
                Cursor = Cursors.Default;

                // Stop function's execution.
                return;
            }

            // Store each line of the unpatched GDI file into an element of "gdiArrayUnpatched".
            string[] gdiArrayUnpatched = File.ReadAllLines(gdiFileUnpatched);

            // Set default GDI compatibility flag to "true".
            bool compatibleUnpatchedGDI = true;

            // Construct "gditools" command for initial unpatched GDI validation.
            string command_VALIDATE_UNPATCHED_GDI = "-i \"" + gdiFileUnpatched + "\"";

            // Perform initial source GDI validation step by executing "gditools.exe".
            System.Diagnostics.Process processValidateUnpatchedGDI = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfoValidateUnpatchedGDI = new System.Diagnostics.ProcessStartInfo();
            startInfoValidateUnpatchedGDI.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfoValidateUnpatchedGDI.FileName = appBaseFolder + "\\tools\\gditools.exe";
            startInfoValidateUnpatchedGDI.Arguments = command_VALIDATE_UNPATCHED_GDI;
            startInfoValidateUnpatchedGDI.RedirectStandardOutput = true;
            startInfoValidateUnpatchedGDI.RedirectStandardError = true;
            startInfoValidateUnpatchedGDI.UseShellExecute = false;
            startInfoValidateUnpatchedGDI.CreateNoWindow = true;
            processValidateUnpatchedGDI.StartInfo = startInfoValidateUnpatchedGDI;
            processValidateUnpatchedGDI.Start();
            processValidateUnpatchedGDI.WaitForExit();

            // Store standard error and standard output of "gditools.exe" from initial unpatched GDI validation.
            var gdiValidateUnpatchedGDIStandardOutput = processValidateUnpatchedGDI.StandardOutput.ReadToEnd();
            var gdiValidateUnpatchedGDIErrorOutput = processValidateUnpatchedGDI.StandardError.ReadToEnd();

            // Update progress bar.
            patchBuildProgressBar.Value += 1;
            patchBuildProgressPercentage.Text = patchBuildProgressBar.Value + "%";

            // Update patching progress details.
            patchBuildProgressDetails.Text = "Verifying integrity of unpatched GDI...";

            // Sleep for half a second.
            wait(500);

            // Update progress bar.
            patchBuildProgressBar.Value += 1;
            patchBuildProgressPercentage.Text = patchBuildProgressBar.Value + "%";

            // Sleep for half a second.
            wait(500);

            // If standard error of "gditools.exe" isn't empty, consider the unpatched GDI incompatible.
            if(!String.IsNullOrEmpty(gdiValidateUnpatchedGDIErrorOutput))
            {
                compatibleUnpatchedGDI = false;
            }
            // Otherwise, proceed with additional GDI validation.
            else
            {
                // Store root path of unpatched GDI.
                gdiBaseFolderUnpatched = Path.GetDirectoryName(gdiFileUnpatched);

                // Iterate through each track in the GDI for validation.
                for(int i = 1; i < gdiArrayUnpatched.Length; i ++)
                {
                    // Extract filename.
                    var trackInfoSanityCheckUnpatchedGDI = gdiArrayUnpatched[i].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                    string trackFilenameSanityCheckUnpatchedGDI = trackInfoSanityCheckUnpatchedGDI[4];
                    string trackFileExtensionSanityCheckUnpatchedGDI = Path.GetExtension(trackFilenameSanityCheckUnpatchedGDI).ToLower();

                    // GDI track file either doesn't exist or has the wrong file extension.
                    if(!File.Exists(gdiBaseFolderUnpatched + "\\" + trackFilenameSanityCheckUnpatchedGDI) || (trackFileExtensionSanityCheckUnpatchedGDI != ".bin" && trackFileExtensionSanityCheckUnpatchedGDI != ".raw"))
                    {
                        // Set flag to "false".
                        compatibleUnpatchedGDI = false;
                    }
                }
            }

            // If the unpatched GDI is malformed or incompatible, throw an error.
            if(!compatibleUnpatchedGDI)
            {
                // Display error message.
                MessageBox.Show("The selected unpatched GDI is either malformed or incompatible.", "Universal Dreamcast Patch Builder", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Hide progress bar and reset it.
                patchBuildProgressBar.Value = 0;
                patchBuildProgressBar.Visible = false;
                patchBuildProgressDetails.Visible = false;
                patchBuildProgressPercentage.Visible = false;

                // Show previously hidden buttons.
                buttonBuildPatch.Visible = true;
                buttonQuit.Visible = true;

                // Re-enable step 1 group.
                groupboxStep1.Enabled = true;
                buttonSelectPatchedGDI.Enabled = false;

                // Change enabled/disabled status of buttons.
                buttonQuit.Enabled = true;
                buttonBuildPatch.Enabled = false;

                // Return to normal logo.
                pictureBox1.Image = pictureBox3.Image;

                // Return to normal cursor.
                Cursor = Cursors.Default;

                // Stop function's execution.
                return;
            }

            // Store each line of the unpatched GDI file into an element of "gdiArrayUnpatched".
            string[] gdiArrayPatched = File.ReadAllLines(gdiFilePatched);

            // Set default GDI compatibility flag to "true".
            bool compatiblePatchedGDI = true;

            // Construct "gditools" command for initial unpatched GDI validation.
            string command_VALIDATE_PATCHED_GDI = "-i \"" + gdiFilePatched + "\"";

            // Perform initial unpatched GDI validation step by executing "gditools.exe".
            System.Diagnostics.Process processValidatePatchedGDI = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfoValidatePatchedGDI = new System.Diagnostics.ProcessStartInfo();
            startInfoValidatePatchedGDI.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfoValidatePatchedGDI.FileName = appBaseFolder + "\\tools\\gditools.exe";
            startInfoValidatePatchedGDI.Arguments = command_VALIDATE_UNPATCHED_GDI;
            startInfoValidatePatchedGDI.RedirectStandardOutput = true;
            startInfoValidatePatchedGDI.RedirectStandardError = true;
            startInfoValidatePatchedGDI.UseShellExecute = false;
            startInfoValidatePatchedGDI.CreateNoWindow = true;
            processValidatePatchedGDI.StartInfo = startInfoValidateUnpatchedGDI;
            processValidatePatchedGDI.Start();
            processValidatePatchedGDI.WaitForExit();

            // Store standard error and standard output of "gditools.exe" from initial patched GDI validation.
            var gdiValidatePatchedGDIStandardOutput = processValidatePatchedGDI.StandardOutput.ReadToEnd();
            var gdiValidatePatchedGDIErrorOutput = processValidatePatchedGDI.StandardError.ReadToEnd();

            // Update progress bar.
            patchBuildProgressBar.Value += 1;
            patchBuildProgressPercentage.Text = patchBuildProgressBar.Value + "%";

            // Update patching progress details.
            patchBuildProgressDetails.Text = "Verifying integrity of patched GDI...";

            // Sleep for half a second.
            wait(500);

            // Update progress bar.
            patchBuildProgressBar.Value += 1;
            patchBuildProgressPercentage.Text = patchBuildProgressBar.Value + "%";

            // Sleep for half a second.
            wait(500);

            // If standard error of "gditools.exe" isn't empty, consider the patched GDI incompatible.
            if(!String.IsNullOrEmpty(gdiValidatePatchedGDIErrorOutput))
            {
                compatiblePatchedGDI = false;
            }
            // Otherwise, proceed with additional GDI validation.
            else
            {
                // Store root path of patched GDI.
                gdiBaseFolderPatched = Path.GetDirectoryName(gdiFilePatched);

                // Iterate through each track in the GDI for validation.
                for(int i = 1; i < gdiArrayPatched.Length; i ++)
                {
                    // Extract filename.
                    var trackInfoSanityCheckPatchedGDI = gdiArrayPatched[i].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                    string trackFilenameSanityCheckPatchedGDI = trackInfoSanityCheckPatchedGDI[4];
                    string trackFileExtensionSanityCheckPatchedGDI = Path.GetExtension(trackFilenameSanityCheckPatchedGDI).ToLower();

                    // GDI track file either doesn't exist or has the wrong file extension.
                    if(!File.Exists(gdiBaseFolderPatched + "\\" + trackFilenameSanityCheckPatchedGDI) || (trackFileExtensionSanityCheckPatchedGDI != ".bin" && trackFileExtensionSanityCheckPatchedGDI != ".raw"))
                    {
                        // Set flag to "false".
                        compatiblePatchedGDI = false;
                    }
                }
            }

            // If the patched GDI is malformed or incompatible, throw an error.
            if(!compatiblePatchedGDI)
            {
                // Display error message.
                MessageBox.Show("The selected patched GDI is either malformed or incompatible.", "Universal Dreamcast Patch Builder", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Hide progress bar and reset it.
                patchBuildProgressBar.Value = 0;
                patchBuildProgressBar.Visible = false;
                patchBuildProgressDetails.Visible = false;
                patchBuildProgressPercentage.Visible = false;

                // Show previously hidden buttons.
                buttonBuildPatch.Visible = true;
                buttonQuit.Visible = true;

                // Re-enable step 1 group.
                groupboxStep1.Enabled = true;
                buttonSelectPatchedGDI.Enabled = false;

                // Change enabled/disabled status of buttons.
                buttonQuit.Enabled = true;
                buttonBuildPatch.Enabled = false;

                // Return to normal logo.
                pictureBox1.Image = pictureBox3.Image;

                // Return to normal cursor.
                Cursor = Cursors.Default;

                // Stop function's execution.
                return;
            }

            // Copy unpatched GDI file to temporary folder.
            File.Copy(gdiFileUnpatched, appTempFolder + "_gdi_unpatched\\disc.gdi");

            // Convert all text in unpatched GDI file to lowercase.
            File.WriteAllText(appTempFolder + "_gdi_unpatched\\disc.gdi", File.ReadAllText(gdiFileUnpatched).ToLower());

            // Calculate GDI-file-copy progress bar interval value.
            int gdiUnpatchedCopyProgress = 15 / (gdiArrayUnpatched.Length - 1);

            // Copy each track file to temporary folder.
            for(int i = 1; i < gdiArrayUnpatched.Length; i ++)
            {
                // Extract filename.
                var trackInfoUnpatched = gdiArrayUnpatched[i].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                string trackFilenameUnpatched = trackInfoUnpatched[4];

                // Update patching progress details.
                patchBuildProgressDetails.Text = "Copying " + trackFilenameUnpatched.ToLower() + " from unpatched GDI...";

                // Sleep for 1 second.
                wait(1000);

                // Copy file, lowercasing its name if necessary.
                File.Copy(gdiBaseFolderUnpatched + "\\" + trackFilenameUnpatched, appTempFolder + "_gdi_unpatched\\" + trackFilenameUnpatched.ToLower());

                // If track count exceeds 30, add "1" to the progress bar for every other file.
                if(i % 2 == 0 && gdiUnpatchedCopyProgress == 0)
                {
                    patchBuildProgressBar.Value += 1;
                }
                // Otherwise, add calculated interval to progress bar.
                else
                {
                    patchBuildProgressBar.Value += gdiUnpatchedCopyProgress;
                }

                // Update patching progress details.
                patchBuildProgressPercentage.Text = patchBuildProgressBar.Value + "%";
            }

            // Copy patched GDI file to temporary folder.
            File.Copy(gdiFilePatched, appTempFolder + "_gdi_patched\\disc.gdi");

            // Convert all text in unpatched GDI file to lowercase.
            File.WriteAllText(appTempFolder + "_gdi_patched\\disc.gdi", File.ReadAllText(gdiFilePatched).ToLower());

            // Calculate GDI-file-copy progress bar interval value.
            int gdiPatchedCopyProgress = 15 / (gdiArrayPatched.Length - 1);

            // Copy each track file to temporary folder.
            for(int i = 1; i < gdiArrayPatched.Length; i ++)
            {
                // Extract filename.
                var trackInfoPatched = gdiArrayPatched[i].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                string trackFilenamePatched = trackInfoPatched[4];

                // Update patching progress details.
                patchBuildProgressDetails.Text = "Copying " + trackFilenamePatched.ToLower() + " from patched GDI...";

                // Sleep for 1 second.
                wait(1000);

                // Copy file, lowercasing its name if necessary.
                File.Copy(gdiBaseFolderPatched + "\\" + trackFilenamePatched, appTempFolder + "_gdi_patched\\" + trackFilenamePatched.ToLower());

                // If track count exceeds 30, add "1" to the progress bar for every other file.
                if(i % 2 == 0 && gdiPatchedCopyProgress == 0)
                {
                    patchBuildProgressBar.Value += 1;
                }
                // Otherwise, add calculated interval to progress bar.
                else
                {
                    patchBuildProgressBar.Value += gdiPatchedCopyProgress;
                }

                // Update patching progress details.
                patchBuildProgressPercentage.Text = patchBuildProgressBar.Value + "%";
            }

            // Sleep for half a second.
            wait(500);

            // Update patching progress details.
            patchBuildProgressDetails.Text = "Extracting unpatched GDI...";

            // Update progress bar.
            patchBuildProgressBar.Value += 6;
            patchBuildProgressPercentage.Text = patchBuildProgressBar.Value + "%";

            // Sleep for half a second.
            wait(500);

            // Declare string to store list of ".iso" files to extract, converted from ".bin".
            string isoExtractionListUnpatched = String.Empty;

            // Declare string to store last data track's LBA.
            string extractionLastDataTrackLBAUnpatched = String.Empty;

            // Create counter for number of data tracks found in the GDI.
            int extractionDataTrackCountUnpatched = 0;

            // Iterate through each track of the GDI to convert data tracks to ".iso" in the temporary extraction folder.
            for(int i = 1; i < gdiArrayUnpatched.Length; i ++)
            {
                // Extract filename and extension.
                var trackInfoExtractionUnpatched = gdiArrayUnpatched[i].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                string trackFilenameExtractionUnpatched = trackInfoExtractionUnpatched[4];
                string trackFileExtensionExtractionUnpatched = Path.GetExtension(trackFilenameExtractionUnpatched).ToLower();

                // If file is a ".bin" and not the first track of the GDI, convert it to ".iso".
                if(trackFileExtensionExtractionUnpatched == ".bin" && trackFilenameExtractionUnpatched.ToLower() != "track01.bin")
                {
                    // Execute "bin2iso.exe" to convert source GDI data track.
                    System.Diagnostics.Process processBIN2ISOUnpatched = new System.Diagnostics.Process();
                    System.Diagnostics.ProcessStartInfo startInfoBIN2ISOUnpatched = new System.Diagnostics.ProcessStartInfo();
                    startInfoBIN2ISOUnpatched.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                    startInfoBIN2ISOUnpatched.FileName = appBaseFolder + "\\tools\\bin2iso.exe";
                    startInfoBIN2ISOUnpatched.Arguments = "\"" + appTempFolder + "_gdi_unpatched\\" + trackFilenameExtractionUnpatched + "\" \"" + appTempFolder + "_extracted_unpatched\\UDP_" + trackFilenameExtractionUnpatched.ToLower().Replace(".bin", ".iso") + "\"";
                    startInfoBIN2ISOUnpatched.UseShellExecute = false;
                    startInfoBIN2ISOUnpatched.CreateNoWindow = true;
                    processBIN2ISOUnpatched.StartInfo = startInfoBIN2ISOUnpatched;
                    processBIN2ISOUnpatched.Start();

                    // Wait for process to exit, checking every half a second.
                    while(!processBIN2ISOUnpatched.HasExited)
                    {
                        wait(500);
                    }

                    // Close process.
                    processBIN2ISOUnpatched.Close();

                    // Append filename to "isoExtractionList".
                    isoExtractionListUnpatched += " UDP_" + trackFilenameExtractionUnpatched.ToLower().Replace(".bin", ".iso");

                    // Store data track's LBA from source GDI into "extractionLastDataTrackLBA".
                    extractionLastDataTrackLBAUnpatched = trackInfoExtractionUnpatched[1].ToString();

                    // Increase data track counter by 1.
                    extractionDataTrackCountUnpatched ++;
                }
            }

            // If more than one data track is found in the source GDI, append its LBA (plus 150) to the "extract.exe" command's arguments.
            if(extractionDataTrackCountUnpatched > 1)
            {
                isoExtractionListUnpatched += " " + (Int32.Parse(extractionLastDataTrackLBAUnpatched) + 150).ToString();
            }

            // Copy "extract.exe" to temporary extraction folder.
            File.Copy(appBaseFolder + "\\tools\\extract.exe", appTempFolder + "_extracted_unpatched\\extract.exe");

            // Execute "extract.exe" to extract the converted ".iso" files.
            System.Diagnostics.Process processExtractUnpatched = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfoExtractUnpatched = new System.Diagnostics.ProcessStartInfo();
            startInfoExtractUnpatched.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfoExtractUnpatched.FileName = appTempFolder + "_extracted_unpatched\\extract.exe";
            startInfoExtractUnpatched.Arguments = isoExtractionListUnpatched;
            startInfoExtractUnpatched.WorkingDirectory = appTempFolder + "_extracted_unpatched\\";
            startInfoExtractUnpatched.UseShellExecute = false;
            startInfoExtractUnpatched.CreateNoWindow = true;
            processExtractUnpatched.StartInfo = startInfoExtractUnpatched;
            processExtractUnpatched.Start();

            // Wait for process to exit, checking every half a second.
            while(!processExtractUnpatched.HasExited)
            {
                wait(500);
            }

            // Close process.
            processExtractUnpatched.Close();

            // Delete "extract.exe" from temporary extraction folder.
            File.Delete(appTempFolder + "_extracted_unpatched\\extract.exe");

            // Delete all converted ".iso" files from the temporary extraction folder.
            Directory.GetFiles(appTempFolder + "_extracted_unpatched\\", "UDP_*.iso", SearchOption.TopDirectoryOnly).ToList().ForEach(File.Delete);

            // User has selected to use unpatched GDI's IP.BIN for patch file, so move it to patch folder.
            if(checkboxUsePatchedIPBIN.Checked == true && dropdownPatchedIPBINSource.SelectedIndex == 1)
            {
                Directory.CreateDirectory(appTempFolder + "_patch\\bootsector");
                File.Move(appTempFolder + "_extracted_unpatched\\IP.BIN", appTempFolder + "_patch\\bootsector\\IP.BIN");
            }
            // Otherwise, delete it.
            else
            {
                File.Delete(appTempFolder + "_extracted_unpatched\\IP.BIN");
            }

            // Sleep for half a second.
            wait(500);

            // Update patching progress details.
            patchBuildProgressDetails.Text = "Extracting patched GDI...";

            // Update progress bar.
            patchBuildProgressBar.Value += 6;
            patchBuildProgressPercentage.Text = patchBuildProgressBar.Value + "%";

            // Sleep for half a second.
            wait(500);

            // Declare string to store list of ".iso" files to extract, converted from ".bin".
            string isoExtractionListPatched = String.Empty;

            // Declare string to store last data track's LBA.
            string extractionLastDataTrackLBPPatched = String.Empty;

            // Create counter for number of data tracks found in the GDI.
            int extractionDataTrackCountPatched = 0;

            // Iterate through each track of the GDI to convert data tracks to ".iso" in the temporary extraction folder.
            for(int i = 1; i < gdiArrayPatched.Length; i ++)
            {
                // Extract filename and extension.
                var trackInfoExtractionPatched = gdiArrayPatched[i].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                string trackFilenameExtractionPatched = trackInfoExtractionPatched[4];
                string trackFileExtensionExtractionPatched = Path.GetExtension(trackFilenameExtractionPatched).ToLower();

                // If file is a ".bin" and not the first track of the GDI, convert it to ".iso".
                if(trackFileExtensionExtractionPatched == ".bin" && trackFilenameExtractionPatched.ToLower() != "track01.bin")
                {
                    // Execute "bin2iso.exe" to convert source GDI data track.
                    System.Diagnostics.Process processBIN2ISOPatched = new System.Diagnostics.Process();
                    System.Diagnostics.ProcessStartInfo startInfoBIN2ISOPatched = new System.Diagnostics.ProcessStartInfo();
                    startInfoBIN2ISOPatched.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                    startInfoBIN2ISOPatched.FileName = appBaseFolder + "\\tools\\bin2iso.exe";
                    startInfoBIN2ISOPatched.Arguments = "\"" + appTempFolder + "_gdi_patched\\" + trackFilenameExtractionPatched + "\" \"" + appTempFolder + "_extracted_patched\\UDP_" + trackFilenameExtractionPatched.ToLower().Replace(".bin", ".iso") + "\"";
                    startInfoBIN2ISOPatched.UseShellExecute = false;
                    startInfoBIN2ISOPatched.CreateNoWindow = true;
                    processBIN2ISOPatched.StartInfo = startInfoBIN2ISOPatched;
                    processBIN2ISOPatched.Start();

                    // Wait for process to exit, checking every half a second.
                    while(!processBIN2ISOPatched.HasExited)
                    {
                        wait(500);
                    }

                    // Close process.
                    processBIN2ISOPatched.Close();

                    // Append filename to "isoExtractionList".
                    isoExtractionListPatched += " UDP_" + trackFilenameExtractionPatched.ToLower().Replace(".bin", ".iso");

                    // Store data track's LBA from source GDI into "extractionLastDataTrackLBA".
                    extractionLastDataTrackLBPPatched = trackInfoExtractionPatched[1].ToString();

                    // Increase data track counter by 1.
                    extractionDataTrackCountPatched ++;
                }
            }

            // If more than one data track is found in the source GDI, append its LBA (plus 150) to the "extract.exe" command's arguments.
            if(extractionDataTrackCountPatched > 1)
            {
                isoExtractionListPatched += " " + (Int32.Parse(extractionLastDataTrackLBPPatched) + 150).ToString();
            }

            // Copy "extract.exe" to temporary extraction folder.
            File.Copy(appBaseFolder + "\\tools\\extract.exe", appTempFolder + "_extracted_patched\\extract.exe");

            // Execute "extract.exe" to extract the converted ".iso" files.
            System.Diagnostics.Process processExtractPatched = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfoExtractPatched = new System.Diagnostics.ProcessStartInfo();
            startInfoExtractPatched.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfoExtractPatched.FileName = appTempFolder + "_extracted_patched\\extract.exe";
            startInfoExtractPatched.Arguments = isoExtractionListPatched;
            startInfoExtractPatched.WorkingDirectory = appTempFolder + "_extracted_patched\\";
            startInfoExtractPatched.UseShellExecute = false;
            startInfoExtractPatched.CreateNoWindow = true;
            processExtractPatched.StartInfo = startInfoExtractPatched;
            processExtractPatched.Start();

            // Wait for process to exit, checking every half a second.
            while(!processExtractPatched.HasExited)
            {
                wait(500);
            }

            // Close process.
            processExtractPatched.Close();

            // Delete "extract.exe" from temporary extraction folder.
            File.Delete(appTempFolder + "_extracted_patched\\extract.exe");

            // Delete all converted ".iso" files from the temporary extraction folder.
            Directory.GetFiles(appTempFolder + "_extracted_patched\\", "UDP_*.iso", SearchOption.TopDirectoryOnly).ToList().ForEach(File.Delete);

            // User has selected to use unpatched GDI's IP.BIN for patch file, so move it to patch folder.
            if(checkboxUsePatchedIPBIN.Checked == true && dropdownPatchedIPBINSource.SelectedIndex == 0)
            {
                Directory.CreateDirectory(appTempFolder + "_patch\\bootsector");
                File.Move(appTempFolder + "_extracted_patched\\IP.BIN", appTempFolder + "_patch\\bootsector\\IP.BIN");
            }
            // Otherwise, delete it.
            else
            {
                File.Delete(appTempFolder + "_extracted_patched\\IP.BIN");
            }

            // Update progress bar.
            patchBuildProgressBar.Value += 5;
            patchBuildProgressPercentage.Text = patchBuildProgressBar.Value + "%";

            // Perform additional IP.BIN patching per user's selection.
            if(checkboxAdditionalPatchingIPBIN.Checked == true && (checkboxRegionFreeIPBIN.Checked == true || checkboxVGAIPBIN.Checked == true || checkboxCustomNameIPBIN.Checked == true))
            {
                // Update patching progress details.
                patchBuildProgressDetails.Text = "Applying patches to IP.BIN...";

                // Open IP.BIN file being used to build patch.
                using(var ipFileStream = File.Open(appTempFolder + "_patch\\bootsector\\IP.BIN", FileMode.Open))
                {
                    // Patch region flag and text.
                    if(checkboxRegionFreeIPBIN.Checked == true)
                    {
                        byte[] regionFlagsJUE = StringToByteArray("4a5545");
                        ipFileStream.Position = 48;
                        ipFileStream.Write(regionFlagsJUE, 0, regionFlagsJUE.Length);

                        byte[] regionStringJ = StringToByteArray("466F72204A4150414E2C54414957414E2C5048494C4950494E45532E");
                        ipFileStream.Position = 14084;
                        ipFileStream.Write(regionStringJ, 0, regionStringJ.Length);

                        byte[] regionStringU = StringToByteArray("466F722055534120616E642043414E4144412E202020202020202020");
                        ipFileStream.Position = 14116;
                        ipFileStream.Write(regionStringU, 0, regionStringU.Length);

                        byte[] regionStringE = StringToByteArray("466F72204555524F50452E2020202020202020202020202020202020");
                        ipFileStream.Position = 14148;
                        ipFileStream.Write(regionStringE, 0, regionStringE.Length);
                    }

                    // Patch VGA flag.
                    if(checkboxVGAIPBIN.Checked == true)
                    {
                        byte[] vgaFlag = StringToByteArray("31");
                        ipFileStream.Position = 61;
                        ipFileStream.Write(vgaFlag, 0, vgaFlag.Length);
                    }

                    // Patch name text.
                    if(checkboxCustomNameIPBIN.Checked == true)
                    {
                        string gameName = textboxGameNameIPBIN.Text;
                        int gameNamePadding = 128 - gameName.Length;

                        for(int i = 0; i < gameNamePadding; i++)
                        {
                            gameName += " ";
                        }

                        byte[] gameNameByteArray = System.Text.Encoding.ASCII.GetBytes(gameName);
                        ipFileStream.Position = 128;
                        ipFileStream.Write(gameNameByteArray, 0, gameNameByteArray.Length);
                    }
                }
            }

            // Sleep for half a second.
            wait(500);

            // Update patching progress details.
            patchBuildProgressDetails.Text = "Building patch data based on modifications to GDI...";

            // Update progress bar.
            patchBuildProgressBar.Value += 5;
            patchBuildProgressPercentage.Text = patchBuildProgressBar.Value + "%";

            // Store recursive list of all files/folders from extracted patched GDI.
            DirectoryInfo diExtractedPatchedGDI = new DirectoryInfo(appTempFolder + "_extracted_patched\\");
            WalkDirectoryTree(diExtractedPatchedGDI);

            // Iterate through each file/folder from extracted patched GDI for comparison to original unpatched GDI.
            for(int i = 0; i < patchedGDIFiles.Count; i ++)
            {
                // Store paths, respective to extracted patched/unpatched GDI files/folders.
                string patchedFullFilePath = patchedGDIFiles[i];
                string patchedFileRelativePath = patchedFullFilePath.Replace(appTempFolder + "_extracted_patched\\", "");
                string patchedFileParentFolder = Path.GetDirectoryName(patchedFullFilePath);
                string unpatchedFullFilePath = patchedFullFilePath.Replace(appTempFolder + "_extracted_patched", appTempFolder + "_extracted_unpatched");
                string unpatchedFileParentFolder = Path.GetDirectoryName(unpatchedFullFilePath);
                string patchFullFilePath = patchedFullFilePath.Replace(appTempFolder + "_extracted_patched", appTempFolder + "_patch");
                string patchFileParentFolder = Path.GetDirectoryName(patchFullFilePath);

                // Current file from extracted patched GDI has a counterpart in original GDI.
                if(File.Exists(unpatchedFullFilePath))
                {
                    // Current file has been modified in patched GDI, so create delta.
                    if(GetMD5HashFromFile(patchedFullFilePath) != GetMD5HashFromFile(unpatchedFullFilePath))
                    {
                        // Ensure parent directory exists by creating it, ignoring errors if already present.
                        LanguageUtils.IgnoreErrors(() => Directory.CreateDirectory(patchFileParentFolder));

                        // Execute process to apply xdelta patch.
                        System.Diagnostics.Process processXDELTA = new System.Diagnostics.Process();
                        System.Diagnostics.ProcessStartInfo startInfoXDELTA = new System.Diagnostics.ProcessStartInfo();
                        startInfoXDELTA.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                        startInfoXDELTA.FileName = appBaseFolder + "\\tools\\xdelta.exe";
                        startInfoXDELTA.Arguments = "-A -e -s \"" + unpatchedFullFilePath + "\" \"" + patchedFullFilePath + "\" \"" + patchFullFilePath + ".xdelta\"";
                        startInfoXDELTA.UseShellExecute = false;
                        startInfoXDELTA.CreateNoWindow = true;
                        processXDELTA.StartInfo = startInfoXDELTA;
                        processXDELTA.Start();

                        // Wait for process to exit, checking every half a second.
                        while(!processXDELTA.HasExited)
                        {
                            wait(500);
                        }

                        // Close process.
                        processXDELTA.Close();

                        // Update progress bar.
                        if(patchBuildProgressBar.Value < 85)
                        {
                            patchBuildProgressBar.Value ++;
                            patchBuildProgressPercentage.Text = patchBuildProgressBar.Value + "%";
                        }
                    }
                }
                // Otherwise, consider it a new file and copy it directly to patch folder.
                else
                {
                    // Ignore "BOOTSECTOR\IP.BIN" if it was erroneously placed in root of patched GDI.
                    if(patchedFileRelativePath.ToUpper() != "BOOTSECTOR\\IP.BIN")
                    {
                        // Ensure parent directory exists by creating it, ignoring errors if already present.
                        LanguageUtils.IgnoreErrors(() => Directory.CreateDirectory(patchFileParentFolder));

                        // Copy new file.
                        File.Copy(patchedFullFilePath, patchFullFilePath);

                        // Update progress bar.
                        if(patchBuildProgressBar.Value < 85)
                        {
                            patchBuildProgressBar.Value ++;
                            patchBuildProgressPercentage.Text = patchBuildProgressBar.Value + "%";
                        }
                    }
                }
            }

            // Sleep for 1 second.
            wait(1000);

            // Update patching progress details.
            patchBuildProgressDetails.Text = "Compiling patch data...";

            // Update progress bar.
            patchBuildProgressBar.Value += 5;
            patchBuildProgressPercentage.Text = patchBuildProgressBar.Value + "%";

            // Delete any previously existing version of the same patch.
            if(File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\" + textboxPatchFilename.Text + ".dcp"))
            {
                File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\" + textboxPatchFilename.Text + ".dcp");
            }

            // Create patch file from patch data.
            System.IO.Compression.ZipFile.CreateFromDirectory(appTempFolder + "_patch", AppDomain.CurrentDomain.BaseDirectory + "\\" + textboxPatchFilename.Text + ".dcp");

            // Update patching progress details.
            patchBuildProgressDetails.Text = "Cleaning up temporary files...";

            // Update progress bar.
            patchBuildProgressBar.Value += 5;
            patchBuildProgressPercentage.Text = patchBuildProgressBar.Value + "%";

            // Perform clean-up.
            Directory.Delete(appTempFolder + "_extracted_unpatched", true);
            Directory.Delete(appTempFolder + "_gdi_unpatched", true);
            Directory.Delete(appTempFolder + "_extracted_patched", true);
            Directory.Delete(appTempFolder + "_gdi_patched", true);
            Directory.Delete(appTempFolder + "_patch", true);

            // Sleep for 1 second.
            wait(1000);

            // Update patching progress details.
            patchBuildProgressDetails.Text = "Done!";

            // Update progress bar.
            patchBuildProgressBar.Value = 100;
            patchBuildProgressPercentage.Text = patchBuildProgressBar.Value + "%";

            // Sleep for 1 second.
            wait(1000);

            // Return to normal cursor.
            Cursor = Cursors.Default;

            // Display final success message.
            MessageBox.Show("Congratulations, the patch was built successfully!  The new DCP has been generated within this application's working directory with the following filename:\n\n" + patchFilename, "Universal Dreamcast Patch Builder", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Hide progress bar.
            patchBuildProgressBar.Visible = false;
            patchBuildProgressDetails.Visible = false;
            patchBuildProgressPercentage.Visible = false;

            // Unhide buttons and toggle enabled/disabled status.
            buttonBuildPatch.Visible = true;
            buttonBuildPatch.Enabled = false;
            buttonQuit.Visible = true;
            buttonQuit.Enabled = true;

            // Restore original logo.
            pictureBox1.Image = pictureBox3.Image;
        }

        // Function to check filename string for invalid characters.
        bool IsValidFilename(string fileName)
        {
            return !fileName.Any(f => Path.GetInvalidFileNameChars().Contains(f));
        }

        // Function to sleep/wait without UI lock.
        public void wait(int milliseconds)
        {
            var waitTimer = new System.Windows.Forms.Timer();

            if(milliseconds == 0 || milliseconds < 0)
            {
                return;
            }

            waitTimer.Interval = milliseconds;
            waitTimer.Enabled = true;
            waitTimer.Start();

            waitTimer.Tick += (s, e) =>
            {
                waitTimer.Enabled = false;
                waitTimer.Stop();
            };

            while(waitTimer.Enabled)
            {
                Application.DoEvents();
            }
        }

        // Main function for recursive file/folder copying.
        public static void RecursiveCopy(string sourceDirectory, string targetDirectory)
        {
            DirectoryInfo directorySource = new DirectoryInfo(sourceDirectory);
            DirectoryInfo directoryTarget = new DirectoryInfo(targetDirectory);

            RecursiveCopyAll(directorySource, directoryTarget);
        }

        // Recursive function for file/folder copying.
        public static void RecursiveCopyAll(DirectoryInfo source, DirectoryInfo target)
        {
            // Copy each file into the new directory.
            foreach(FileInfo fileInfo in source.GetFiles())
            {
                fileInfo.CopyTo(Path.Combine(target.FullName, fileInfo.Name), true);
            }

            // Copy each subdirectory using recursion.
            foreach(DirectoryInfo directorySourceSubDirectory in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDirectory = target.CreateSubdirectory(directorySourceSubDirectory.Name);
                RecursiveCopyAll(directorySourceSubDirectory, nextTargetSubDirectory);
            }
        }

        // Return MD5 hash for a specified file.
        private static string GetMD5HashFromFile(string fileName)
        {
            using(var md5 = MD5.Create())
            {
                using(var stream = File.OpenRead(fileName))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", string.Empty);
                }
            }
        }

        // Recursively traverse directory tree, ignoring empty folders.
        static void WalkDirectoryTree(System.IO.DirectoryInfo root)
        {
            System.IO.FileInfo[] files = null;
            System.IO.DirectoryInfo[] subDirs = null;

            // First, process all the files directly under this folder.
            files = root.GetFiles("*.*");

            if(files != null)
            {
                foreach(System.IO.FileInfo fi in files)
                {
                    patchedGDIFiles.Add(fi.FullName);
                }

                // Now find all the subdirectories under this directory.
                subDirs = root.GetDirectories();

                foreach(System.IO.DirectoryInfo dirInfo in subDirs)
                {
                    // Resursive call for each subdirectory.
                    WalkDirectoryTree(dirInfo);
                }
            }
        }

        // Return byte array of a hex string.
        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        private void CheckboxUsePatchedIPBIN_CheckedChanged(object sender, EventArgs e)
        {
            // Enable dropdown for selecting IP.BIN source.
            if(checkboxUsePatchedIPBIN.Checked == true)
            {
                dropdownPatchedIPBINSource.Enabled = true;

                if(dropdownPatchedIPBINSource.SelectedItem == null)
                {
                    dropdownPatchedIPBINSource.SelectedIndex = 0;
                }

                checkboxAdditionalPatchingIPBIN.Enabled = true;
                checkboxCustomNameIPBIN.Enabled = true;
            }
            // Disable dropdown for selecting IP.BIN source.
            else
            {
                dropdownPatchedIPBINSource.Enabled = false;
                checkboxAdditionalPatchingIPBIN.Enabled = false;
                checkboxAdditionalPatchingIPBIN.Checked = false;
                checkboxRegionFreeIPBIN.Enabled = false;
                checkboxRegionFreeIPBIN.Checked = false;
                checkboxVGAIPBIN.Enabled = false;
                checkboxVGAIPBIN.Checked = false;
                checkboxCustomNameIPBIN.Enabled = false;
                checkboxCustomNameIPBIN.Checked = false;
                ipbinTree1.BackColor = System.Drawing.Color.Silver;
                ipbinTree2.BackColor = System.Drawing.Color.Silver;
                ipbinTree3.BackColor = System.Drawing.Color.Silver;
                ipbinTree4.BackColor = System.Drawing.Color.Silver;
                ipbinTree5.BackColor = System.Drawing.Color.Silver;
            }
        }

        private void CheckboxAdditionalPatchingIPBIN_CheckedChanged(object sender, EventArgs e)
        {
            // Enable IP.BIN patching options.
            if(checkboxAdditionalPatchingIPBIN.Checked == true)
            {
                checkboxRegionFreeIPBIN.Enabled = true;
                checkboxVGAIPBIN.Enabled = true;
                ipbinTree1.BackColor = System.Drawing.Color.Black;
                ipbinTree2.BackColor = System.Drawing.Color.Black;
                ipbinTree3.BackColor = System.Drawing.Color.Black;
            }
            // Disable IP.BIN patching options.
            else
            {
                checkboxRegionFreeIPBIN.Enabled = false;
                checkboxRegionFreeIPBIN.Checked = false;
                checkboxVGAIPBIN.Enabled = false;
                checkboxVGAIPBIN.Checked = false;
                ipbinTree1.BackColor = System.Drawing.Color.Silver;
                ipbinTree2.BackColor = System.Drawing.Color.Silver;
                ipbinTree3.BackColor = System.Drawing.Color.Silver;
            }
        }

        private void CheckboxCustomNameIPBIN_CheckedChanged(object sender, EventArgs e)
        {
            // Enable custom game name in IP.BIN.
            if(checkboxCustomNameIPBIN.Checked == true)
            {
                textboxGameNameIPBIN.Enabled = true;
                ipbinTree4.BackColor = System.Drawing.Color.Black;
                ipbinTree5.BackColor = System.Drawing.Color.Black;
            }
            // Disable custom game name in IP.BIN.
            else
            {
                textboxGameNameIPBIN.Enabled = false;
                ipbinTree4.BackColor = System.Drawing.Color.Silver;
                ipbinTree5.BackColor = System.Drawing.Color.Silver;
            }
        }
    }

    public static class LanguageUtils
    {
        /// <summary>
        /// Runs an operation and ignores any Exceptions that occur.
        /// Returns true or falls depending on whether catch was
        /// triggered
        /// </summary>
        /// <param name="operation">lambda that performs an operation that might throw</param>
        /// <returns></returns>
        public static bool IgnoreErrors(Action operation)
        {
            if(operation == null)
                return false;
            try
            {
                operation.Invoke();
            }
            catch
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Runs an function that returns a value and ignores any Exceptions that occur.
        /// Returns true or falls depending on whether catch was
        /// triggered
        /// </summary>
        /// <param name="operation">parameterless lamda that returns a value of T</param>
        /// <param name="defaultValue">Default value returned if operation fails</param>
        public static T IgnoreErrors<T>(Func<T> operation, T defaultValue = default(T))
        {
            if(operation == null)
                return defaultValue;

            T result;
            try
            {
                result = operation.Invoke();
            }
            catch
            {
                result = defaultValue;
            }

            return result;
        }
    }
}
