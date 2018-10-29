using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using System.Threading.Tasks;
using Plugin.FilePicker.Abstractions;
using Android.Provider;
using Android.Database;
using Android;
using Android.Content.PM;
using Android.Widget;

namespace Plugin.FilePicker
{
    [Activity(ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.ScreenSize)]
    [Android.Runtime.Preserve(AllMembers = true)]
    public class FilePickerActivity : Activity
    {
        private Context context;
        private Task<Tuple<string, bool>> path;
        private const int REQUEST_STORAGE = 1;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Bundle b = (savedInstanceState ?? Intent.Extras);

            if (Build.VERSION.SDK_INT >= 23) 
            { 
                if (CheckSelfPermission(Manifest.Permission.ReadExternalStorage) == (int)Permission.Granted)
                {
                    launchPicker();
                }
                else
                {
                    RequestPermissions(new String[] { Manifest.Permission.ReadExternalStorage }, REQUEST_STORAGE);
                }
            }
            else
            {
                launchPicker();
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            if(requestCode == REQUEST_STORAGE) 
            {
                if(grantResults[0] == Permission.Granted) 
                {
                    launchPicker();
                } 
                else
                {
                    Toast.MakeText(this, "File Permission Denied.", ToastLength.Long).Show();
                    OnFilePickCancelled();
                    Finish();
                }
            }
        }


            private void launchPicker() {

            this.context = Android.App.Application.Context;
            Intent intent = new Intent(Intent.ActionGetContent);
            intent.SetType("*/*");

            intent.AddCategory(Intent.CategoryOpenable);
            try
            {
                StartActivityForResult(Intent.CreateChooser(intent, "Selecione o arquivo a ser enviado"),
                      0);
            }
            catch (System.Exception exAct)
            {
                System.Diagnostics.Debug.Write(exAct);
            }
        }

        protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (resultCode == Result.Canceled)
            {
                OnFilePickCancelled();
                Finish();
            }
            else
            {
                try
                {
                    var _uri = data.Data;

                    string filePath = IOUtil.getPath(this.context, _uri);

                    if (string.IsNullOrEmpty(filePath))
                        filePath = _uri.Path;

                    var file = IOUtil.readFile(filePath);
                    if (file.Length == 0)
                    {
                        //Did not successfully read from path - attempt to read from stream (using URI)
                        var stream = context.ContentResolver.OpenInputStream(_uri);
                        file = IOUtil.readStream(stream);
                    }

                    var fileName = GetFileName(this.context, _uri);

                    OnFilePicked(new FilePickerEventArgs(file, fileName));
                }
                catch (System.Exception readEx)
                {
                    OnFilePickCancelled();
                    System.Diagnostics.Debug.Write(readEx);
                }
                finally
                {
                    Finish();
                }
            }
        }

        string GetFileName(Context context, Android.Net.Uri uri)
        {

            String[] projection = { MediaStore.MediaColumns.DisplayName };

            ContentResolver cr = context.ContentResolver;
            string name = "";
            ICursor metaCursor = cr.Query(uri, projection, null, null, null);
            if (metaCursor != null)
            {
                try
                {
                    if (metaCursor.MoveToFirst())
                    {
                        name = metaCursor.GetString(0);
                    }
                }
                finally
                {
                    metaCursor.Close();
                }
            }
            return name;
        }

        internal static event EventHandler<FilePickerEventArgs> FilePicked;
        internal static event EventHandler<EventArgs> FilePickCancelled;

        private static void OnFilePickCancelled()
        {
            FilePickCancelled?.Invoke(null, null);
        }

        private static void OnFilePicked(FilePickerEventArgs e)
        {
            var picked = FilePicked;
            if (picked != null)
                picked(null, e);
        }
    }
}