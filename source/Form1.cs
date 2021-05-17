using System;
using System.IO;
using System.Windows.Forms;
using System.Security.AccessControl;

namespace UniversalDreamcastPatcher
{
    public partial class Form1 : Form
    {
        public string gdiFile;
        public string patchFile;
        public string patchFilename;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.CenterToScreen();

            // Perform initial sanity check for helper tools.
            string missingFiles = String.Empty;

            if(!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\tools\\buildgdi.exe"))
            {
                missingFiles = missingFiles + "\n - buildgdi.exe";
            }

            if(!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\tools\\convertredumptogdi.exe"))
            {
                missingFiles = missingFiles + "\n - convertredumptogdi.exe";
            }

            if(!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\tools\\gditools.exe"))
            {
                missingFiles = missingFiles + "\n - buigditoolsldgdi.exe";
            }

            if(!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\tools\\DiscUtils.dll"))
            {
                missingFiles = missingFiles + "\n - DiscUtils.dll";
            }

            if(!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\tools\\python27.dll"))
            {
                missingFiles = missingFiles + "\n - python27.dll";
            }

            if(!String.IsNullOrEmpty(missingFiles))
            {
                MessageBox.Show("One or more required files is missing from the \"tools\" folder:" + missingFiles, "Universal Dreamcast Patcher", MessageBoxButtons.OK, MessageBoxIcon.Error);

                Application.Exit();
            }
        }

        private void ButtonSelectGDI_Click(object sender, EventArgs e)
        {
            OpenFileDialog openGDI = new OpenFileDialog();
            openGDI.Filter = "GDI and CUE files (*.gdi;*.cue)|*.gdi;*.cue";
            DialogResult resultOpenGDI = openGDI.ShowDialog();

            if(resultOpenGDI == DialogResult.OK)
            {
                gdiFile = openGDI.FileName;

                buttonApplyPatch.Visible = false;
                buttonApplyPatch.Enabled = false;
                buttonSelectPatch.Visible = true;
                buttonSelectPatch.Enabled = true;
            }
        }

        private void ButtonSelectPatch_Click(object sender, EventArgs e)
        {
            OpenFileDialog openPatch = new OpenFileDialog();
            openPatch.Filter = "DCP files (*.dcp)|*.dcp";
            DialogResult resultOpenPatch = openPatch.ShowDialog();

            if(resultOpenPatch == DialogResult.OK)
            {
                patchFile = openPatch.FileName;
                patchFilename = Path.GetFileNameWithoutExtension(patchFile);

                buttonSelectPatch.Visible = false;
                buttonSelectPatch.Enabled = false;
                buttonApplyPatch.Visible = true;
                buttonApplyPatch.Enabled = true;
            }
        }

        private void ButtonApplyPatch_Click(object sender, EventArgs e)
        {
            // Disable buttons.
            buttonSelectGDI.Enabled = false;
            buttonApplyPatch.Enabled = false;
            buttonQuit.Enabled = false;

            // Prompt for confirmation to proceed with patching.
            DialogResult confirmPatch = MessageBox.Show("The selected source disc image will not be overwritten. The new patched GDI will be generated in the following new folder within this application's directory:\n\n" + patchFilename + " [GDI]\n\nA message will appear once the patching process has completed." + "\n\n" + "Are you ready to proceed?", "Universal Dreamcast Patcher", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            // User selected "Yes".
            if(confirmPatch == DialogResult.Yes)
            {
                // Hide buttons.
                buttonSelectGDI.Visible = false;
                buttonApplyPatch.Visible = false;
                buttonQuit.Visible = false;

                // Show progress bar and progress details.
                patchingProgressBar.Visible = true;
                patchingProgressDetails.Visible = true;
                patchingProgressPercentage.Visible = true;

                // Show wait cursor.
                Cursor.Current = Cursors.WaitCursor;

                // Sleep for half a second.
                wait(500);

                // Update progress bar.
                patchingProgressBar.Value += 5;
                patchingProgressPercentage.Text = patchingProgressBar.Value + "%";

                // Store filenames and paths.
                string gdiFilename = "disc.gdi";
                string gdiBaseFolder = Path.GetDirectoryName(gdiFile);
                string appBaseFolder = AppDomain.CurrentDomain.BaseDirectory;
                string folderGUID = Guid.NewGuid().ToString();
                string appTempFolder = appBaseFolder + folderGUID;

                // Set default CUE source image flag to "false".
                bool sourceImageIsCUE = false;

                // Create temporary folders for patched files, copied GDI, and extracted GDI.
                DirectorySecurity securityRules = new DirectorySecurity();
                Directory.CreateDirectory(appTempFolder);
                Directory.CreateDirectory(appTempFolder + "_extracted");
                Directory.CreateDirectory(appTempFolder + "_patch");

                // If source disc image is in CUE format, convert to GDI before initiating the patching process.
                if(Path.GetExtension(gdiFile).ToLower() == ".cue")
                {
                    // Update CUE source image flag to "true".
                    sourceImageIsCUE = true;

                    // Update patching progress details.
                    patchingProgressDetails.Text = "Converting source disc image to GDI...";

                    // Sleep for 1 second.
                    wait(1000);

                    // Convert CUE disc image to GDI by executing "convertredumptogdi.exe".
                    System.Diagnostics.Process processConvert = new System.Diagnostics.Process();
                    System.Diagnostics.ProcessStartInfo convertRedumpToGDI = new System.Diagnostics.ProcessStartInfo();
                    convertRedumpToGDI.FileName = Path.Combine(appBaseFolder, "tools", "convertredumptogdi.exe");
                    convertRedumpToGDI.Arguments = "\"" + gdiFile + "\" \"" + appTempFolder + "\"";
                    convertRedumpToGDI.RedirectStandardOutput = true;
                    convertRedumpToGDI.UseShellExecute = false;
                    convertRedumpToGDI.CreateNoWindow = true;
                    processConvert.StartInfo = convertRedumpToGDI;
                    processConvert.Start();
                    processConvert.WaitForExit();

                    // Set default conversion complete flag to "false".
                    bool redumpConversionComplete = false;

                    // Create counter for conversion completion check.
                    int conversionCheckCount = 0;

                    // Continuously check for CUE to GDI conversion completion.
                    while(!redumpConversionComplete)
                    {
                        // Increase conversion completion check counter by 1.
                        conversionCheckCount ++;

                        // Store fole count for the temporary folder.
                        string[] temporaryFiles = Directory.GetFiles(appTempFolder);

                        // Check if "_complete_" file exists in the temporary folder.
                        if(File.Exists(appTempFolder + "\\_complete_"))
                        {
                            // Delete "_complete_" file in the temporary folder.
                            File.Delete(appTempFolder + "\\_complete_");

                            // Update value of "gdiFile" to point to the newly converted GDI.
                            gdiFile = appTempFolder + "\\disc.gdi";

                            // Update value of "gdiBaseFolder" to point to the temporary location containing the newly converted GDI.
                            gdiBaseFolder = appTempFolder;

                            // Set the conversion complete flag to "true" to break the loop.
                            redumpConversionComplete = true;
                        }
                        // If "_complete_" file doesn't exist, ensure the temporary folder isn't empty after 5 seconds, as this indicates a failed conversion.
                        else if(conversionCheckCount >= 5 && temporaryFiles.Length == 0)
                        {
                            // Display error message.
                            MessageBox.Show("The selected source CUE is either malformed or incompatible.", "Universal Dreamcast Patcher", MessageBoxButtons.OK, MessageBoxIcon.Error);

                            // Hide progress bar and reset it.
                            patchingProgressBar.Value = 0;
                            patchingProgressBar.Visible = false;
                            patchingProgressDetails.Visible = false;
                            patchingProgressPercentage.Visible = false;

                            // Show previously hidden buttons.
                            buttonSelectGDI.Visible = true;
                            buttonApplyPatch.Visible = true;
                            buttonQuit.Visible = true;

                            // Change enabled/disabled status of buttons.
                            buttonSelectGDI.Enabled = true;
                            buttonApplyPatch.Enabled = false;
                            buttonQuit.Enabled = true;

                            // Remove temporary GDI folder and all of its contents.
                            Directory.Delete(appTempFolder, true);

                            // Remove temporary extracted GDI folder and all of its contents.
                            Directory.Delete(appTempFolder + "_extracted", true);

                            // Remove temporary extracted patch folder and all of its contents.
                            Directory.Delete(appTempFolder + "_patch", true);

                            // Stop function's execution.
                            return;
                        }

                        // Sleep for 1 second before next loop iteration.
                        wait(1000);
                    }

                    // Update progress bar.
                    patchingProgressBar.Value += 5;
                    patchingProgressPercentage.Text = patchingProgressBar.Value + "%";
                }

                // Store each line of the source GDI file into an element of "gdiArray".
                string[] gdiArray = File.ReadAllLines(gdiFile);

                // Set default GDI compatibility flag to "true".
                bool compatibleGDI = true;

                // Construct "gditools" command for initial source GDI validation.
                string command_VALIDATE = "-command \"& '" + appBaseFolder + "tools\\gditools.exe' -i '" + gdiFile + "'\"";

                // Perform initial source GDI validation step by executing "gditools.exe" via PowerShell.
                System.Diagnostics.Process processValidate = new System.Diagnostics.Process();
                System.Diagnostics.ProcessStartInfo startInfoValidate = new System.Diagnostics.ProcessStartInfo();
                startInfoValidate.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                startInfoValidate.FileName = "powershell.exe";
                startInfoValidate.Arguments = command_VALIDATE;
                startInfoValidate.RedirectStandardOutput = true;
                startInfoValidate.RedirectStandardError = true;
                startInfoValidate.UseShellExecute = false;
                startInfoValidate.CreateNoWindow = true;
                processValidate.StartInfo = startInfoValidate;
                processValidate.Start();
                processValidate.WaitForExit();

                // Store standard error output of "gditools.exe" from initial source GDI validation.
                var gdiValidateErrorOutput = processValidate.StandardError.ReadToEnd();

                // Update progress bar.
                patchingProgressBar.Value += 5;
                patchingProgressPercentage.Text = patchingProgressBar.Value + "%";

                // Update patching progress details.
                patchingProgressDetails.Text = "Verifying integrity of source GDI...";

                // Sleep for half a second.
                wait(500);

                // Update progress bar.
                patchingProgressBar.Value += 5;
                patchingProgressPercentage.Text = patchingProgressBar.Value + "%";

                // Sleep for half a second.
                wait(500);

                // If standard error output of "gditools.exe" isn't empty, consider the source GDI incompatible.
                if(!String.IsNullOrEmpty(gdiValidateErrorOutput))
                {
                    compatibleGDI = false;
                }
                // Otherwise, proceed with additional GDI validation.
                else
                {
                    // Iterate through each track in the GDI for validation.
                    for(int i = 1; i < gdiArray.Length; i++)
                    {
                        // Extract filename.
                        var trackInfoSanityCheck = gdiArray[i].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                        string trackFilenameSanityCheck = trackInfoSanityCheck[4];
                        string trackFileExtensionSanityCheck = Path.GetExtension(trackFilenameSanityCheck).ToLower();

                        // GDI track file either doesn't exist or has the wrong file extension.
                        if(!File.Exists(gdiBaseFolder + "\\" + trackFilenameSanityCheck) || (trackFileExtensionSanityCheck != ".bin" && trackFileExtensionSanityCheck != ".raw"))
                        {
                            // Set flag to "false".
                            compatibleGDI = false;
                        }
                    }
                }

                // If the source GDI is malformed or incompatible, throw an error.
                if(!compatibleGDI)
                {
                    // Hide progress bar and reset it.
                    patchingProgressBar.Value = 0;
                    patchingProgressBar.Visible = false;
                    patchingProgressDetails.Visible = false;
                    patchingProgressPercentage.Visible = false;

                    // Show previously hidden buttons.
                    buttonSelectGDI.Visible = true;
                    buttonApplyPatch.Visible = true;
                    buttonQuit.Visible = true;

                    // Change enabled/disabled status of buttons.
                    buttonSelectGDI.Enabled = true;
                    buttonApplyPatch.Enabled = false;
                    buttonQuit.Enabled = true;

                    // Remove temporary GDI folder and all of its contents.
                    Directory.Delete(appTempFolder, true);

                    // Remove temporary extracted GDI folder and all of its contents.
                    Directory.Delete(appTempFolder + "_extracted", true);

                    // Remove temporary extracted patch folder and all of its contents.
                    Directory.Delete(appTempFolder + "_patch", true);

                    // Display error message.
                    MessageBox.Show("The selected source GDI is either malformed or incompatible.", "Universal Dreamcast Patcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                // Otherwise, proceed with the patching process.
                else
                {
                    // Skip GDI copying if source image is CUE, as it's already in the temporary folder from conversion.
                    if(sourceImageIsCUE)
                    {
                        // Update patching progress details.
                        patchingProgressDetails.Text = "Verifying integrity of converted GDI...";

                        // Update progress bar.
                        patchingProgressBar.Value += 30;
                        patchingProgressPercentage.Text = patchingProgressBar.Value + "%";

                        // Sleep for 1 second.
                        wait(1000);
                    }
                    // Otherwise, proceed with copying source GDI to the temporary folder.
                    else
                    {
                        // Copy source GDI file to temporary folder.
                        File.Copy(gdiFile, appTempFolder + "\\disc.gdi");

                        // Calculate GDI-file-copy progress bar interval value.
                        int gdiCopyProgress = 30 / (gdiArray.Length - 1);

                        // Copy each track file to temporary folder.
                        for(int i = 1; i < gdiArray.Length; i++)
                        {
                            // Extract filename.
                            var trackInfo = gdiArray[i].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                            string trackFilename = trackInfo[4];

                            // Update patching progress details.
                            patchingProgressDetails.Text = "Copying " + trackFilename.ToLower() + "...";

                            // Sleep for 1 second.
                            wait(1000);

                            // Copy file.
                            File.Copy(gdiBaseFolder + "\\" + trackFilename, appTempFolder + "\\" + trackFilename.ToString());

                            // Add calculated interval to progress bar.
                            patchingProgressBar.Value += gdiCopyProgress;
                            patchingProgressPercentage.Text = patchingProgressBar.Value + "%";
                        }
                    }

                    // Sleep for half a second.
                    wait(500);

                    // Update patching progress details.
                    patchingProgressDetails.Text = "Extracting GDI...";

                    // Update progress bar.
                    patchingProgressBar.Value += 13;
                    patchingProgressPercentage.Text = patchingProgressBar.Value + "%";

                    // Sleep for half a second.
                    wait(500);

                    // Construct "gditools" command for GDI extraction.
                    string command_EXTRACT = "-command \"& '" + appBaseFolder + "tools\\gditools.exe' -i '" + appBaseFolder + folderGUID + "\\" + gdiFilename + "' --data-folder '..\\" + folderGUID + "_extracted' -b '..\\" + folderGUID + "_extracted\\bootsector\\IP.BIN' --extract-all --silent\"";

                    // Execute "gditools.exe" via PowerShell to extract the selected GDI to the temporary folder.
                    System.Diagnostics.Process processExtract = new System.Diagnostics.Process();
                    System.Diagnostics.ProcessStartInfo startInfoExtract = new System.Diagnostics.ProcessStartInfo();
                    startInfoExtract.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                    startInfoExtract.FileName = "powershell.exe";
                    startInfoExtract.Arguments = command_EXTRACT;
                    processExtract.StartInfo = startInfoExtract;
                    processExtract.Start();
                    processExtract.WaitForExit();

                    // Update patching progress details.
                    patchingProgressDetails.Text = "Patching extracted GDI files with new data...";

                    // Update progress bar.
                    patchingProgressBar.Value += 13;
                    patchingProgressPercentage.Text = patchingProgressBar.Value + "%";

                    // Sleep for half a second.
                    wait(500);

                    // Attempt to extract .DCP patch file and copy its contents into the temporary extracted GDI folder.
                    try
                    {
                        System.IO.Compression.ZipFile.ExtractToDirectory(patchFile, appTempFolder + "_patch");
                        RecursiveCopy(appTempFolder + "_patch", appTempFolder + "_extracted");
                    }
                    // Otherwise, if the .DCP patch file is corrupt or otherwise malformed, throw an error.
                    catch
                    {
                        // Display error message.
                        MessageBox.Show("The selected DCP patch file is either corrupt or incompatible.", "Universal Dreamcast Patcher", MessageBoxButtons.OK, MessageBoxIcon.Error);

                        // Hide progress bar and reset it.
                        patchingProgressBar.Value = 0;
                        patchingProgressBar.Visible = false;
                        patchingProgressDetails.Visible = false;
                        patchingProgressPercentage.Visible = false;

                        // Show previously hidden buttons.
                        buttonSelectGDI.Visible = true;
                        buttonApplyPatch.Visible = true;
                        buttonQuit.Visible = true;

                        // Change enabled/disabled status of buttons.
                        buttonSelectGDI.Enabled = true;
                        buttonApplyPatch.Enabled = false;
                        buttonQuit.Enabled = true;

                        // Remove temporary GDI folder and all of its contents.
                        Directory.Delete(appTempFolder, true);

                        // Remove temporary extracted GDI folder and all of its contents.
                        Directory.Delete(appTempFolder + "_extracted", true);

                        // Remove temporary extracted patch folder and all of its contents.
                        Directory.Delete(appTempFolder + "_patch", true);

                        // Stop function's execution.
                        return;
                    }

                    // Set hardcoded timestamp for all folders and subfolders in game data before building GDI.
                    DateTime hardcodedDirectoryTimestamp = new DateTime(1999, 9, 9, 0, 0, 0, DateTimeKind.Utc);

                    // Store array of all folders and subfolders in game data.
                    string[] gameDataFolders = Directory.GetDirectories(appTempFolder + "_extracted", "*", SearchOption.AllDirectories);

                    // Iterate through "gameDataFolders" array to apply timestamps to all folders and subfolders in game data before building GDI.
                    for(int i = 0; i < gameDataFolders.Length; i ++)
                    {
                        Directory.SetCreationTimeUtc(gameDataFolders[i], hardcodedDirectoryTimestamp);
                        Directory.SetLastAccessTimeUtc(gameDataFolders[i], hardcodedDirectoryTimestamp);
                        Directory.SetLastWriteTimeUtc(gameDataFolders[i], hardcodedDirectoryTimestamp);
                    }

                    // Store array of all files across entire tree of game data.
                    string[] gameDataFiles = Directory.GetFiles(appTempFolder + "_extracted", "*", SearchOption.AllDirectories);

                    // Iterate through "gameDataFolders" array to apply timestamps to all folders and subfolders in game data before building GDI.
                    for(int i = 0; i < gameDataFiles.Length; i++)
                    {
                        File.SetCreationTimeUtc(gameDataFiles[i], hardcodedDirectoryTimestamp);
                        File.SetLastAccessTimeUtc(gameDataFiles[i], hardcodedDirectoryTimestamp);
                        File.SetLastWriteTimeUtc(gameDataFiles[i], hardcodedDirectoryTimestamp);
                    }

                    // Update patching progress details.
                    patchingProgressDetails.Text = "Building patched GDI...";

                    // Update progress bar.
                    patchingProgressBar.Value += 14;
                    patchingProgressPercentage.Text = patchingProgressBar.Value + "%";

                    // Sleep for half a second.
                    wait(500);

                    // Construct "buildgdi" command for GDI rebuild.
                    string command_BUILD = "-command \"& '" + appBaseFolder + "tools\\buildgdi.exe' -data '" + appBaseFolder + folderGUID + "_extracted' -ip '" + appBaseFolder + folderGUID + "_extracted\\bootsector\\IP.BIN' -output '" + appBaseFolder + folderGUID + "' -gdi '" + appBaseFolder + folderGUID + "\\" + gdiFilename + "' -raw";
                    
                    // If the source GDI contains contains CDDA, append those tracks to "buildgdi" command.
                    if(File.Exists(appBaseFolder + folderGUID + "\\track04.raw"))
                    {
                        // Add flag to "buildgdi" command signifying presence of CDDA.
                        command_BUILD = command_BUILD + " -cdda";

                        // Store all GDI track filenames in "cddaTracks" array.
                        string[] cddaTracks = Directory.GetDirectories(appTempFolder + "_extracted", "track*.raw", SearchOption.TopDirectoryOnly);

                        // Iterate through each track file.
                        for(int i = 0; i < cddaTracks.Length; i ++)
                        {
                            // Store track number without extension or prepended "track" string.
                            string cddaTrackFilename = Path.GetFileNameWithoutExtension(cddaTracks[i]);
                            cddaTrackFilename = cddaTrackFilename.Replace("track", "");

                            // If track number is greater than or equal to 4, append it to the "buildgdi" command.
                            if(Int32.Parse(cddaTrackFilename) >= 4)
                            {
                                command_BUILD = command_BUILD + " '" + cddaTracks[i] + "'";
                            }
                        }
                    }

                    // Finish constructing "buildgdi" command.
                    command_BUILD = command_BUILD + "\"";

                    // Execute "buildgdi.exe" via PowerShell to build the patched GDI.
                    System.Diagnostics.Process processBuild = new System.Diagnostics.Process();
                    System.Diagnostics.ProcessStartInfo startInfoBuild = new System.Diagnostics.ProcessStartInfo();
                    startInfoBuild.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                    startInfoBuild.FileName = "powershell.exe";
                    startInfoBuild.Arguments = command_BUILD;
                    processBuild.StartInfo = startInfoBuild;
                    processBuild.Start();
                    processBuild.WaitForExit();

                    // Remove temporary extracted GDI folder and all of its contents.
                    Directory.Delete(appTempFolder + "_extracted", true);

                    // Remove temporary extracted patch folder and all of its contents.
                    Directory.Delete(appTempFolder + "_patch", true);

                    // Remove patched GDI folder and all of its contents if it already exists.
                    if(Directory.Exists(appBaseFolder + "\\" + patchFilename + " [GDI]"))
                    {
                        Directory.Delete(appBaseFolder + "\\" + patchFilename + " [GDI]", true);
                    }

                    // Rename temporary GDI folder based on the name of the patch.
                    Directory.Move(appTempFolder, appBaseFolder + "\\" + patchFilename + " [GDI]");

                    // Update patching progress details.
                    patchingProgressDetails.Text = "Done!";

                    // Complete progress bar.
                    patchingProgressBar.Value = 100;
                    patchingProgressPercentage.Text = patchingProgressBar.Value + "%";

                    // Sleep for 1 second.
                    wait(1000);

                    // Display final success message.
                    MessageBox.Show("Congratulations, the patch was successfully applied!\n\nThe new GDI is in this application's directory within the following folder:\n\n" + patchFilename + " [GDI]\n\n" + "Have fun!", "Universal Dreamcast Patcher", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // Hide the progress bar and progress details.
                    patchingProgressBar.Visible = false;
                    patchingProgressDetails.Visible = false;
                    patchingProgressPercentage.Visible = false;

                    // Remove wait curson.
                    Cursor.Current = Cursors.Default;

                    // Unide buttons.
                    buttonSelectGDI.Visible = true;
                    buttonApplyPatch.Visible = true;
                    buttonQuit.Visible = true;

                    // Re-enable the "Quit" button.
                    buttonQuit.Enabled = true;
                }
            }
            // User selected "No".
            else
            {
                // Re-enable buttons.
                buttonSelectGDI.Enabled = true;
                buttonApplyPatch.Enabled = true;
                buttonQuit.Enabled = true;
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

        private void PictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void Label1_Click(object sender, EventArgs e)
        {

        }

        private void PictureBox1_Click_1(object sender, EventArgs e)
        {

        }

        private void Label2_Click(object sender, EventArgs e)
        {

        }

        private void ButtonQuit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void Label1_Click_1(object sender, EventArgs e)
        {

        }
    }
}
