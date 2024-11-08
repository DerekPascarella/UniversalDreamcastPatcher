using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;

namespace UniversalDreamcastPatcher
{
    public partial class Form1 : Form
    {
        public string gdiFile;
        public string patchFile;
        public string patchFilename;
        private static List<string> patchedGDIFiles = new List<string>();

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
                missingFiles = missingFiles + "\n - gditools.exe";
            }

            if(!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\tools\\python27.dll"))
            {
                missingFiles = missingFiles + "\n - python27.dll";
            }

            if(!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\tools\\DiscUtils.dll"))
            {
                missingFiles = missingFiles + "\n - DiscUtils.dll";
            }

            if(!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\tools\\extract.exe"))
            {
                missingFiles = missingFiles + "\n - extract.exe";
            }

            if(!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\tools\\bin2iso.exe"))
            {
                missingFiles = missingFiles + "\n - bin2iso.exe";
            }

            if(!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\tools\\xdelta.exe"))
            {
                missingFiles = missingFiles + "\n - xdelta.exe";
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

                // Update progress bar.
                patchingProgressBar.Value += 5;
                patchingProgressPercentage.Text = patchingProgressBar.Value + "%";

                // Store filenames and paths.
                string gdiFilename = "disc.gdi";
                string gdiBaseFolder = Path.GetDirectoryName(gdiFile);
                string appBaseFolder = AppDomain.CurrentDomain.BaseDirectory;
                string folderGUID = Guid.NewGuid().ToString();
                string appTempFolder = Path.GetTempPath() + "_UDP_" + folderGUID;

                // Set default CUE source image flag to "false".
                bool sourceImageIsCUE = false;

                // Create temporary folders for patched files, copied GDI, and extracted GDI.
                try
                {
                    Directory.CreateDirectory(appTempFolder);
                    Directory.CreateDirectory(appTempFolder + "_extracted");
                    Directory.CreateDirectory(appTempFolder + "_patch");
                }
                // Otherwise, display an error if folder creation fails.
                catch
                {
                    // Display error message.
                    MessageBox.Show("Unable to create necessary temporary folders!\n\nTry running Universal Dreamcast Patcher as Administrator.", "Universal Dreamcast Patcher", MessageBoxButtons.OK, MessageBoxIcon.Error);

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

                    // Return to normal logo.
                    pictureBox1.Image = pictureBox3.Image;

                    // Stop function's execution.
                    return;
                }

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

                            // For fixing broken track filenames, store each line of the source GDI file into an element of "gdiArrayFix".
                            string[] gdiArrayFix = File.ReadAllLines(gdiFile);

                            // Iterate through each track of the GDI to fix potentially broken filenames.
                            for(int i = 1; i < gdiArrayFix.Length; i ++)
                            {
                                // Extract filename and extension.
                                var trackInfoFix = gdiArrayFix[i].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                                string trackFilenameFix = trackInfoFix[4];
                                string trackFileExtensionFix = Path.GetExtension(trackFilenameFix).ToLower();
                                string trackNumberFix = trackFilenameFix.Replace("track", "");
                                trackNumberFix = trackNumberFix.Replace(trackFileExtensionFix, "");

                                // Track number length is erroneously greater than three digits.
                                if(trackNumberFix.Length > 2)
                                {
                                    // Remove first digit from track number.
                                    trackNumberFix = trackNumberFix.Substring(1);

                                    // Store fixed track filename.
                                    string newTrackFilenameFix = "track" + trackNumberFix + trackFileExtensionFix;

                                    // Rename track file.
                                    File.Move(appTempFolder + "\\" + trackFilenameFix, appTempFolder + "\\" + newTrackFilenameFix);

                                    // Replace reference to filename in GDI.
                                    string gdiContents = File.ReadAllText(gdiFile);
                                    gdiContents = gdiContents.Replace(trackFilenameFix, newTrackFilenameFix);
                                    File.WriteAllText(gdiFile, gdiContents);
                                }
                            }

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

                            // Return to normal logo.
                            pictureBox1.Image = pictureBox3.Image;

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
                string command_VALIDATE = "-i \"" + gdiFile + "\"";

                // Perform initial source GDI validation step by executing "gditools.exe".
                System.Diagnostics.Process processValidate = new System.Diagnostics.Process();
                System.Diagnostics.ProcessStartInfo startInfoValidate = new System.Diagnostics.ProcessStartInfo();
                startInfoValidate.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                startInfoValidate.FileName = appBaseFolder + "\\tools\\gditools.exe";
                startInfoValidate.Arguments = command_VALIDATE;
                startInfoValidate.RedirectStandardOutput = true;
                startInfoValidate.RedirectStandardError = true;
                startInfoValidate.UseShellExecute = false;
                startInfoValidate.CreateNoWindow = true;
                processValidate.StartInfo = startInfoValidate;
                processValidate.Start();
                processValidate.WaitForExit();

                // Store standard error and standard output of "gditools.exe" from initial source GDI validation.
                var gdiValidateStandardOutput = processValidate.StandardOutput.ReadToEnd();
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

                // If standard error of "gditools.exe" isn't empty, consider the source GDI incompatible.
                if(!String.IsNullOrEmpty(gdiValidateErrorOutput))
                {
                    compatibleGDI = false;
                }
                // Otherwise, proceed with additional GDI validation.
                else
                {
                    // Iterate through each track in the GDI for validation.
                    for(int i = 1; i < gdiArray.Length; i ++)
                    {
                        // Extract filename.
                        var trackInfoSanityCheck = gdiArray[i].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                        string trackFilenameSanityCheck = trackInfoSanityCheck[4];
                        string trackFileExtensionSanityCheck = Path.GetExtension(trackFilenameSanityCheck).ToLower();

                        // GDI track file either doesn't exist or has the wrong file extension.
                        if(!File.Exists(gdiBaseFolder + "\\" + trackFilenameSanityCheck) || (trackFileExtensionSanityCheck != ".bin" && trackFileExtensionSanityCheck != ".iso" && trackFileExtensionSanityCheck != ".raw"))
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

                    // Return to normal logo.
                    pictureBox1.Image = pictureBox3.Image;

                    // Remove temporary GDI folder and all of its contents.
                    Directory.Delete(appTempFolder, true);

                    // Remove temporary extracted GDI folder and all of its contents.
                    Directory.Delete(appTempFolder + "_extracted", true);

                    // Remove temporary extracted patch folder and all of its contents.
                    Directory.Delete(appTempFolder + "_patch", true);

                    // Display error message.
                    MessageBox.Show("The selected source GDI is either malformed or incompatible.", "Universal Dreamcast Patcher", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    // Stop function's execution.
                    return;
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

                        // Convert all text in GDI file to lowercase.
                        File.WriteAllText(appTempFolder + "\\disc.gdi", File.ReadAllText(gdiFile).ToLower());

                        // Calculate GDI-file-copy progress bar interval value.
                        int gdiCopyProgress = 30 / (gdiArray.Length - 1);

                        // Copy each track file to temporary folder.
                        for(int i = 1; i < gdiArray.Length; i ++)
                        {
                            // Extract filename.
                            var trackInfo = gdiArray[i].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                            string trackFilename = trackInfo[4];

                            // Update patching progress details.
                            patchingProgressDetails.Text = "Copying " + trackFilename.ToLower() + "...";

                            // Sleep for 1 second.
                            wait(1000);

                            // Copy file, lowercasing its name if necessary.
                            File.Copy(gdiBaseFolder + "\\" + trackFilename, appTempFolder + "\\" + trackFilename.ToLower());

                            // If track count exceeds 30, add "1" to the progress bar for every other file.
                            if(i % 2 == 0 && gdiCopyProgress == 0)
                            {
                                patchingProgressBar.Value += 1;
                            }
                            // Otherwise, add calculated interval to progress bar.
                            else
                            {
                                patchingProgressBar.Value += gdiCopyProgress;
                            }

                            // Update patching progress details.
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

                    // Declare string to store list of ".iso" files to extract, converted from ".bin".
                    string isoExtractionList = String.Empty;

                    // Declare string to store last data track's LBA.
                    string extractionLastDataTrackLBA = String.Empty;

                    // Create counter for number of data tracks found in the GDI.
                    int extractionDataTrackCount = 0;

                    // Iterate through each track of the GDI, converting data tracks to ".iso" in the temporary extraction folder.
                    for(int i = 1; i < gdiArray.Length; i ++)
                    {
                        // Extract filename and extension.
                        var trackInfoExtraction = gdiArray[i].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                        string trackFilenameExtraction = trackInfoExtraction[4];
                        string trackFileExtensionExtraction = Path.GetExtension(trackFilenameExtraction).ToLower();

                        // If file is a ".bin" and not the first track of the GDI, convert it to ".iso".
                        if(trackFileExtensionExtraction == ".bin" && trackFilenameExtraction.ToLower() != "track01.bin")
                        {
                            // Execute "bin2iso.exe" to convert source GDI data track.
                            System.Diagnostics.Process processBIN2ISO = new System.Diagnostics.Process();
                            System.Diagnostics.ProcessStartInfo startInfoBIN2ISO = new System.Diagnostics.ProcessStartInfo();
                            startInfoBIN2ISO.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                            startInfoBIN2ISO.FileName = appBaseFolder + "\\tools\\bin2iso.exe";
                            startInfoBIN2ISO.Arguments = "\"" + appTempFolder + "\\" + trackFilenameExtraction + "\" \"" + appTempFolder + "_extracted\\UDP_" + trackFilenameExtraction.ToLower().Replace(".bin", ".iso") + "\"";
                            startInfoBIN2ISO.UseShellExecute = false;
                            startInfoBIN2ISO.CreateNoWindow = true;
                            processBIN2ISO.StartInfo = startInfoBIN2ISO;
                            processBIN2ISO.Start();

                            // Wait for process to exit, checking every half a second.
                            while(!processBIN2ISO.HasExited)
                            {
                                wait(500);
                            }

                            // Close process.
                            processBIN2ISO.Close();
                        }

                        // Append data track files to list for extraction.
                        if((trackFileExtensionExtraction == ".iso" || trackFileExtensionExtraction == ".bin") && trackFilenameExtraction.ToLower() != "track01.bin" && trackFilenameExtraction.ToLower() != "track01.iso")
                        {
                            // Rename ".iso" file that was not renamed automatically during conversion from ".bin".
                            if(trackFileExtensionExtraction == ".iso")
                            {
                                File.Move(appTempFolder + "\\" + trackFilenameExtraction, appTempFolder + "_extracted\\UDP_" + trackFilenameExtraction.ToLower());
                            }

                            // Append filename to "isoExtractionList".
                            isoExtractionList += " UDP_" + trackFilenameExtraction.ToLower().Replace(".bin", ".iso");

                            // Store data track's LBA from source GDI into "extractionLastDataTrackLBA".
                            extractionLastDataTrackLBA = trackInfoExtraction[1].ToString();

                            // Increase data track counter by 1.
                            extractionDataTrackCount ++;
                        }
                    }

                    // If more than one data track is found in the source GDI, append its LBA (plus 150) to the "extract.exe" command's arguments.
                    if(extractionDataTrackCount > 1)
                    {
                        isoExtractionList += " " + (Int32.Parse(extractionLastDataTrackLBA) + 150).ToString();
                    }

                    // Copy "extract.exe" to temporary extraction folder.
                    File.Copy(appBaseFolder + "\\tools\\extract.exe", appTempFolder + "_extracted\\extract.exe");

                    // Execute "extract.exe" to extract the converted ".iso" files.
                    System.Diagnostics.Process processExtract = new System.Diagnostics.Process();
                    System.Diagnostics.ProcessStartInfo startInfoExtract = new System.Diagnostics.ProcessStartInfo();
                    startInfoExtract.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                    startInfoExtract.FileName = appTempFolder + "_extracted\\extract.exe";
                    startInfoExtract.Arguments = isoExtractionList;
                    startInfoExtract.WorkingDirectory = appTempFolder + "_extracted\\";
                    startInfoExtract.UseShellExecute = false;
                    startInfoExtract.CreateNoWindow = true;
                    processExtract.StartInfo = startInfoExtract;
                    processExtract.Start();

                    // Wait for process to exit, checking every half a second.
                    while(!processExtract.HasExited)
                    {
                        wait(500);
                    }

                    // Close process.
                    processExtract.Close();

                    // Delete "extract.exe" from temporary extraction folder.
                    File.Delete(appTempFolder + "_extracted\\extract.exe");

                    // Delete all data track files from the temporary extraction folder.
                    Directory.GetFiles(appTempFolder + "_extracted\\", "UDP_*.iso", SearchOption.TopDirectoryOnly).ToList().ForEach(File.Delete);
                    Directory.GetFiles(appTempFolder + "_extracted\\", "UDP_*.bin", SearchOption.TopDirectoryOnly).ToList().ForEach(File.Delete);

                    // Remove "bootsector" folder and all of its contents from temporary extraction folder if it already exists.
                    if (Directory.Exists(appTempFolder + "_extracted\\bootsector"))
                    {
                        Directory.Delete(appTempFolder + "_extracted\\bootsector", true);
                    }

                    // Inside temporary extraction folder, create "bootsector" folder and move "IP.BIN" into it.
                    Directory.CreateDirectory(appTempFolder + "_extracted\\bootsector");
                    File.Move(appTempFolder + "_extracted\\IP.BIN", appTempFolder + "_extracted\\bootsector\\IP.BIN");

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

                        // Store recursive list of all files/folders from extracted patched GDI so that xdeltas can be applied.
                        DirectoryInfo diExtractedPatchedGDI = new DirectoryInfo(appTempFolder + "_extracted");
                        WalkDirectoryTree(diExtractedPatchedGDI);

                        // Iterate through each file/folder from extracted patched GDI so that xdeltas can be applied.
                        for(int i = 0; i < patchedGDIFiles.Count; i ++)
                        {
                            // Store full path of current file.
                            string patchedFullFilePath = patchedGDIFiles[i];

                            // Store relative filename of current file being patched, then update patching progress details.
                            string currentPatchingFile = patchedFullFilePath.Replace(appTempFolder + "_extracted\\", "").Replace(".xdelta", "");
                            patchingProgressDetails.Text = "Patching " + currentPatchingFile + "...\n";

                            // Current file is an xdelta patch.
                            if(patchedFullFilePath.ToLower().EndsWith(".xdelta"))
                            {
                                // Store full path of corresponding original file.
                                string patchedFullFilePath_ORIGINAL = patchedFullFilePath.Replace(".xdelta", "");

                                // Corresponding original file exists.
                                if(File.Exists(patchedFullFilePath_ORIGINAL))
                                {
                                    // Execute process to apply xdelta patch.
                                    System.Diagnostics.Process processXDELTA = new System.Diagnostics.Process();
                                    System.Diagnostics.ProcessStartInfo startInfoXDELTA = new System.Diagnostics.ProcessStartInfo();
                                    startInfoXDELTA.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                                    startInfoXDELTA.FileName = appBaseFolder + "\\tools\\xdelta.exe";
                                    startInfoXDELTA.Arguments = "-d -s \"" + patchedFullFilePath_ORIGINAL + "\" \"" + patchedFullFilePath + "\" \"" + patchedFullFilePath_ORIGINAL + ".new\"";
                                    startInfoXDELTA.RedirectStandardOutput = true;
                                    startInfoXDELTA.RedirectStandardError = true;
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

                                    // Store error output of "xdelta.exe".
                                    var xdeltaErrorOutput = processValidate.StandardError.ReadToEnd();

                                    // If error output isn't empty, an error has occurred and patching should not proceed.
                                    if(!String.IsNullOrEmpty(xdeltaErrorOutput))
                                    {
                                        // Display error message.
                                        MessageBox.Show("The selected source disc image contains a different version of one or more files than what's expected by the selected DCP patch.  Patching process cannot proceed.", "Universal Dreamcast Patcher", MessageBoxButtons.OK, MessageBoxIcon.Error);

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

                                        // Return to normal logo.
                                        pictureBox1.Image = pictureBox3.Image;

                                        // Remove temporary GDI folder and all of its contents.
                                        Directory.Delete(appTempFolder, true);

                                        // Remove temporary extracted GDI folder and all of its contents.
                                        Directory.Delete(appTempFolder + "_extracted", true);

                                        // Remove temporary extracted patch folder and all of its contents.
                                        Directory.Delete(appTempFolder + "_patch", true);

                                        // Stop function's execution.
                                        return;
                                    }

                                    // Delete original file.
                                    File.Delete(patchedFullFilePath_ORIGINAL);

                                    // Delete xdelta file.
                                    File.Delete(patchedFullFilePath);

                                    // Rename patched file.
                                    File.Move(patchedFullFilePath_ORIGINAL + ".new", patchedFullFilePath_ORIGINAL);
                                }
                                // Corresponding original file does not exist.
                                else
                                {
                                    // Display error message.
                                    MessageBox.Show("The selected DCP patch file contains instructions to modify files not present on the selected source disc image.  Patching process cannot proceed.", "Universal Dreamcast Patcher", MessageBoxButtons.OK, MessageBoxIcon.Error);

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

                                    // Return to normal logo.
                                    pictureBox1.Image = pictureBox3.Image;

                                    // Remove temporary GDI folder and all of its contents.
                                    Directory.Delete(appTempFolder, true);

                                    // Remove temporary extracted GDI folder and all of its contents.
                                    Directory.Delete(appTempFolder + "_extracted", true);

                                    // Remove temporary extracted patch folder and all of its contents.
                                    Directory.Delete(appTempFolder + "_patch", true);

                                    // Stop function's execution.
                                    return;
                                }
                            }
                        }
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

                        // Return to normal logo.
                        pictureBox1.Image = pictureBox3.Image;

                        // Remove temporary GDI folder and all of its contents.
                        Directory.Delete(appTempFolder, true);

                        // Remove temporary extracted GDI folder and all of its contents.
                        Directory.Delete(appTempFolder + "_extracted", true);

                        // Remove temporary extracted patch folder and all of its contents.
                        Directory.Delete(appTempFolder + "_patch", true);

                        // Stop function's execution.
                        return;
                    }

                    // Update patching progress details.
                    patchingProgressDetails.Text = "Patching extracted GDI files with new data...";

                    // Set hardcoded timestamp for all folders and subfolders in game data before building GDI.
                    DateTime hardcodedDirectoryTimestamp = new DateTime(1999, 9, 9, 12, 12, 12, DateTimeKind.Utc);

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
                    for(int i = 0; i < gameDataFiles.Length; i ++)
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

                    // Move "bootsector" folder to temporary folder location.
                    Directory.Move(appTempFolder + "_extracted\\bootsector", appTempFolder + "_bootsector");

                    // Construct "buildgdi" command for GDI rebuild.
                    string command_BUILD = "-data \"" + appTempFolder + "_extracted\" -ip \"" + appTempFolder + "_bootsector\\IP.BIN\" -output \"" + appTempFolder + "\" -gdi \"" + appTempFolder + "\\" + gdiFilename + "\" -raw";

                    // If the source GDI contains contains CDDA, append those tracks to "buildgdi" command.
                    if(File.Exists(appTempFolder + "\\track04.raw"))
                    {
                        // Add flag to "buildgdi" command signifying presence of CDDA.
                        command_BUILD = command_BUILD + " -cdda";

                        // Store all GDI track filenames in "cddaTracks" array.
                        string[] cddaTracks = Directory.GetFiles(appTempFolder, "track*.raw", SearchOption.TopDirectoryOnly);

                        // Iterate through each track file.
                        for(int i = 0; i < cddaTracks.Length; i ++)
                        {
                            // Store track number without extension or prepended "track" string.
                            string cddaTrackFilename = Path.GetFileNameWithoutExtension(cddaTracks[i]);
                            cddaTrackFilename = cddaTrackFilename.Replace("track", "");

                            // If track number is greater than or equal to 4, append it to the "buildgdi" command.
                            if(Int32.Parse(cddaTrackFilename) >= 4)
                            {
                                command_BUILD = command_BUILD + " \"" + cddaTracks[i] + "\"";
                            }
                        }
                    }

                    // Execute "buildgdi.exe" to build the patched GDI.
                    System.Diagnostics.Process processBuild = new System.Diagnostics.Process();
                    System.Diagnostics.ProcessStartInfo startInfoBuild = new System.Diagnostics.ProcessStartInfo();
                    startInfoBuild.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                    startInfoBuild.FileName = appBaseFolder + "tools\\buildgdi.exe";
                    startInfoBuild.Arguments = command_BUILD;
                    startInfoBuild.RedirectStandardError = true;
                    startInfoBuild.UseShellExecute = false;
                    startInfoBuild.CreateNoWindow = true;
                    processBuild.StartInfo = startInfoBuild;
                    processBuild.Start();

                    // Wait for process to exit, checking every half a second.
                    while(!processBuild.HasExited)
                    {
                        wait(500);
                    }

                    // Store error output of "buildgdi.exe".
                    var gdiBuildErrorOutput = processBuild.StandardError.ReadToEnd();

                    // Close process.
                    processBuild.Close();

                    // If error output isn't empty, show message, clean-up files, and reset UI.
                    if(!String.IsNullOrEmpty(gdiBuildErrorOutput))
                    {
                        // Display error message.
                        MessageBox.Show("An unknown error occurred when attempting to build patched GDI.\n\nTry again with a different source disc image.", "Universal Dreamcast Patcher", MessageBoxButtons.OK, MessageBoxIcon.Error);

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

                        // Return to normal logo.
                        pictureBox1.Image = pictureBox3.Image;

                        // Remove temporary GDI folder and all of its contents.
                        Directory.Delete(appTempFolder, true);

                        // Remove temporary extracted GDI folder and all of its contents.
                        Directory.Delete(appTempFolder + "_extracted", true);

                        // Remove temporary extracted patch folder and all of its contents.
                        Directory.Delete(appTempFolder + "_patch", true);

                        // Stop function's execution.
                        return;
                    }
                    else
                    {

                        // Remove temporary extracted GDI folder and all of its contents.
                        Directory.Delete(appTempFolder + "_extracted", true);

                        // Remove temporary extracted patch folder and all of its contents.
                        Directory.Delete(appTempFolder + "_patch", true);

                        // Remove temporary IP.BIN folder and all of its contents.
                        Directory.Delete(appTempFolder + "_bootsector", true);

                        // Remove patched GDI folder and all of its contents if it already exists.
                        if(Directory.Exists(appBaseFolder + "\\" + patchFilename + " [GDI]"))
                        {
                            Directory.Delete(appBaseFolder + "\\" + patchFilename + " [GDI]", true);
                        }

                        // Rename temporary GDI folder based on the name of the patch.
                        Directory.CreateDirectory(appBaseFolder + "\\" + patchFilename + " [GDI]");
                        RecursiveCopy(appTempFolder, appBaseFolder + "\\" + patchFilename + " [GDI]");
                        Directory.Delete(appTempFolder, true);

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

                        // Remove wait cursor.
                        Cursor.Current = Cursors.Default;

                        // Unide buttons.
                        buttonSelectGDI.Visible = true;
                        buttonApplyPatch.Visible = true;
                        buttonQuit.Visible = true;

                        // Re-enable the "Quit" button.
                        buttonQuit.Enabled = true;

                        // Return to normal logo.
                        pictureBox1.Image = pictureBox3.Image;
                    }
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

        private void PictureBox3_Click(object sender, EventArgs e)
        {

        }
    }
}