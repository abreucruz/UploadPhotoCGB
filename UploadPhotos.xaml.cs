using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using SQLite.Net;

using XLabs.Platform;
using XLabs.Platform.Services.Geolocation;

using XLabs.Forms.Mvvm;
using XLabs.Ioc;
using XLabs.Platform.Device;
using XLabs.Platform.Services.Media;
using DataNuage.Azure.Blob;
using System.IO;
using System.Threading;

using SQLite.Net;
using SQLite.Net.Attributes;
using Xamarin.Forms;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.Sync;
using Newtonsoft.Json;

/********************************************************************************************************
Alex Abreu
11/1/2016
Example for CBG how to upload photo
This example was created using Xamarin and Xaml
*********************************************************************************************************/
namespace GoDiamondApp
{
    public partial class UploadPhotos : ContentPage
    {
        SQLiteConnection db;
        private Stream strImageStream;
        string str_EventID = "";

        private IMediaPicker _mediaPicker;
        private ImageSource _imageSource;
        private Command _selectPictureCommand;
        PhotoCloud objEventX;
        private string strImagePath;

        public ImageSource ImageSource
        {
            get
            {
                return _imageSource;
            }
            set
            {
                _imageSource = value;
            }
        }

        //Option when the user tab to find image
        public Command SelectPictureCommand
        {
            get
            {
                return _selectPictureCommand ?? (_selectPictureCommand = new Command(
                                                                           async () => await SelectPicture(),
                                                                           () => true));
            }
        }

        private void Setup()
        {
            if (_mediaPicker != null)
            {
                return;
            }
            var device = Resolver.Resolve<IDevice>();
            _mediaPicker = DependencyService.Get<IMediaPicker>() ?? device.MediaPicker;
        }

        public static byte[] ReadFully(Stream input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }

        //Process to select picture from device
        private async Task SelectPicture()
        {
            Setup();

            ImageSource = null;
            try
            {
                var mediaFile = await _mediaPicker.SelectPhotoAsync(new CameraMediaStorageOptions
                {
                    DefaultCamera = CameraDevice.Front
                });

                strImageStream = mediaFile.Source;
                byte[] bufferx;

                bufferx = ReadFully(mediaFile.Source);

                imgPicture.Source = ImageSource.FromStream(() => new MemoryStream(bufferx));

                strImagePath = mediaFile.Path;

                if (!string.IsNullOrEmpty(strImagePath))
                {
                    imgPicture.HeightRequest = 300;
                    imgPicture.WidthRequest = 300;
                }

            }
            catch (System.Exception ex)
            {
                DisplayAlert("Alert", ex.Message, "OK");
            }
        }


        public UploadPhotos(PhotoCloud objevent)
        {
            InitializeComponent();

            objEventX = objevent;
            str_EventID = objEventX.ID;

            PopulateFields(objEventX);

        }

        //Process to read the image from the Server and assigned to the Image Display
        async void LoadImageFromCloud(string strImage)
        {
            aiProgress.IsVisible = true;
            aiProgress.IsRunning = true;

            var abs = new AzureBlobStorage(Config.strBlobAccount, Config.strBlobAccessKey);
            var t = await abs.GetObjectAsByteArrayAsync(Config.strBlobImage, strImage);
            imgPicture.Source = ImageSource.FromStream(() => new MemoryStream(t));

            aiProgress.IsVisible = false;
            aiProgress.IsRunning = false;
        }

        //procedure to show the fields to the user
        void PopulateFields(PhotoCloud obj)
        {
            txtTittle.Text = objEventX.Title;
            txtDetail.Text = objEventX.SpeakerDesc;

            if (strImagePath.IndexOf(".") < 1)
            {
                LoadImageFromCloud(strImagePath);
            }
        }

        void btnSave_click(object sender, EventArgs args)
        {
            Save_Click();
        }

        void btnCancel_click(object sender, EventArgs args)
        {
            Navigation.PopModalAsync();
        }

        void btnPicture_click(object sender, EventArgs args)
        {
            SelectPicture();
        }

        async void Save_Click()
        {
            btnSave.IsEnabled = false;
                if (strImageStream != null)
                {
                    if (strImageStream.Length > 0)
                    {
                        await LoadImage(strImageStream);
                    }
                }

                save(strImagePath);

            btnSave.IsEnabled = true;
        }

        //Save data to the cloud
        async void save(string strUploadedImageName)
        {

            if (string.IsNullOrEmpty(str_EventID))
            {
                var eventTable = new PhotoCloud
                {
                    //Create for first time the photo title and Description
                    Title = txtTittle.Text.ToString(),
                    SpeakerDesc = txtDetail.Text.ToString(),
                    ImagePath = strUploadedImageName,
                };

                await Config.Update_EventCloud(eventTable);
            }
            else
            {
                //Modify the photo title and Description
                objEventX.Title = txtTittle.Text.ToString();
                objEventX.SpeakerName = txtDetail.Text.ToString();
                objEventX.ImagePath = strUploadedImageName;

                await Config.Update_EventCloud(objEventX);
            }

        }

        //Procedure to save the image on the cloud server
        async Task LoadImage(Stream strFilename)
        {
            CancellationTokenSource cts = null;

            string bucket = "images";
            string strName;
            Random randon = new Random();

            strImagePath = "";

            //Generate random filename to the image with stamp date
            strName = DateTime.Now.ToString();
            strName = strName.Replace("/", "");
            strName = strName.Replace(":", "");
            strName = strName.Replace(" ", "");
            strName += randon.Next(150000);

            byte[] buffer = ReadFully(strFilename);

            var abs = new AzureBlobStorage(Config.strBlobAccount, Config.strBlobAccessKey);

            cts = new CancellationTokenSource();

            uint longitud;
            longitud = Convert.ToUInt32(buffer.Length);

            aiProgress.IsRunning = true;

            try
            {
                await abs.PutObjectAsync(
                       bucket,
                       strName,
                       buffer, contentType: "image/jpeg",
                       cancellationToken: cts.Token,
                       progress: l => pbProgress.ProgressTo((100 * l) / buffer.Length, longitud, Easing.Linear));
            }
            catch (OperationCanceledException)
            {
                aiProgress.IsRunning = false;
            }

            aiProgress.IsRunning = false;
            strImagePath = strName;

        }

        //Procedure to delete the photo from the server
        void btnDelete_click(object sender, EventArgs args)
        {
            DeleteEvent();
        }

        //Delete the title and description from server
        async void DeleteEvent()
        {
            if (!string.IsNullOrEmpty(str_EventID))
            {
                var result = await DisplayAlert("Question?", "Delete this Record?", "Yes", "No");
                if (result == true)
                {
                    Config.Delete_EventCloud(objEventX);
                    Navigation.PopModalAsync();
                }
            }

        }


    }
}

//Class to store title and Description
public class PhotoCloud
{
    [PrimaryKey, AutoIncrement]
    public string ID { get; set; }

    [JsonProperty(PropertyName = "title")]
    public string Title { get; set; }

    [JsonProperty(PropertyName = "speakerdesc")]
    public string SpeakerDesc { get; set; }

    [JsonProperty(PropertyName = "imagepath")]
    public String ImagePath { get; set; }

}

