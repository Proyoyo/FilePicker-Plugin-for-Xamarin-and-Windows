using System;
using System.Threading.Tasks;
using UIKit;
using MobileCoreServices;
using Foundation;
using System.Threading;

using Plugin.FilePicker.Abstractions;
using System.IO;
using System.Linq;

namespace Plugin.FilePicker
{
	/// <summary>
	/// Implementation for FilePicker
	/// </summary>
    public class FilePickerImplementation : NSObject, IUIDocumentPickerDelegate, IFilePicker
	{

		private int requestId;
		private TaskCompletionSource<FileData> completionSource;

		public EventHandler<FilePickerEventArgs> handler
		{
			get;
			set;

		}

		private void OnFilePicked(FilePickerEventArgs e)
		{
			var picked = handler;
			if (picked != null)
				picked(null, e);
		}

		/*public void DidPickDocumentPicker(UIDocumentMenuViewController documentMenu, UIDocumentPickerViewController documentPicker)
		{
		//	documentPicker.DidPickDocument += DocumentPicker_DidPickDocument;
		//	documentPicker.WasCancelled += DocumentPicker_WasCancelled;

			UIApplication.SharedApplication.KeyWindow.RootViewController.PresentViewController(documentPicker, true, null);

		}*/

        public void DidPickDocument(UIDocumentPickerViewController sender, NSUrl url)
		{
			//var securityEnabled = e.Url.StartAccessingSecurityScopedResource();

			var doc = new UIDocument(url);

			var data = NSData.FromUrl(url);

			byte[] dataBytes = new byte[data.Length];

			System.Runtime.InteropServices.Marshal.Copy(data.Bytes, dataBytes, 0, Convert.ToInt32(data.Length));

			string filename = doc.LocalizedName;
			var pathname = doc.FileUrl?.ToString();

			if (filename == null)
			{
				var filesplit = pathname?.LastIndexOf('/') ?? 0;

				filename = pathname?.Substring(filesplit + 1);
				string normalizedFilename = filename.Replace("%20", " ");
				OnFilePicked(new FilePickerEventArgs(dataBytes, normalizedFilename));
			}
			else 
			{
				OnFilePicked(new FilePickerEventArgs(dataBytes, filename));
			}

		}

		public void DocumentPicker_WasCancelled(object sender, EventArgs e)
		{
			var tcs = Interlocked.Exchange(ref completionSource, null);
			tcs?.SetResult(null);
		}

		public async Task<FileData> PickFile()
		{
			var media = await TakeMediaAsync();

			return media;
		}

		private Task<FileData> TakeMediaAsync()
		{

			int id = GetRequestId();

			var ntcs = new TaskCompletionSource<FileData>(id);
			if (Interlocked.CompareExchange(ref this.completionSource, ntcs, null) != null)
				throw new InvalidOperationException("Only one operation can be active at a time");

			var allowedUTIs = new string[] {
				UTType.UTF8PlainText,
				UTType.PlainText,
				UTType.RTF,
				UTType.PNG,
				UTType.Text,
				UTType.PDF,
				UTType.Image,
				UTType.UTF16PlainText,
				UTType.FileURL,
				"com.microsoft.word.doc",
				"org.openxmlformats.wordprocessingml.document",
				UTType.Spreadsheet
			};

            UIDocumentPickerViewController importMenu =
                new UIDocumentPickerViewController(allowedUTIs, UIDocumentPickerMode.Import);
			importMenu.Delegate = this;

			UIApplication.SharedApplication.KeyWindow.RootViewController.PresentViewController(importMenu, true, null);

			handler = null;

			handler = (s, e) =>
			{
				var tcs = Interlocked.Exchange(ref this.completionSource, null);

				tcs.SetResult(new FileData
				{
					DataArray = e.FileByte,
					FileName = e.FileName
				});
			};


			return completionSource.Task;

		}

        public void WasCancelled(UIDocumentPickerViewController documentMenu)
		{
			var tcs = Interlocked.Exchange(ref completionSource, null);
			tcs?.SetResult(null);
		}

		private int GetRequestId()
		{
			int id = this.requestId;
			if (this.requestId == Int32.MaxValue)
				this.requestId = 0;
			else
				this.requestId++;

			return id;
		}

		public async Task<bool> SaveFile(FileData fileToSave)
		{
			try
			{
				var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
				var fileName = Path.Combine(documents, fileToSave.FileName);

				File.WriteAllBytes(fileName, fileToSave.DataArray);

				return true;
			}
			catch (Exception ex)
			{
				return false;
			}
		}

		public void OpenFile(NSUrl fileUrl)
		{
			var docControl = UIDocumentInteractionController.FromUrl(fileUrl);

			var window = UIApplication.SharedApplication.KeyWindow;
			var subViews = window.Subviews;
			var lastView = subViews.Last();
			var frame = lastView.Frame;

			docControl.PresentOpenInMenu(frame, lastView, true);
		}

		public void OpenFile(string fileToOpen)
		{
			var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

			var fileName = Path.Combine(documents, fileToOpen);

			if (NSFileManager.DefaultManager.FileExists(fileName))
			{
				var url = new NSUrl(fileName, true);
				OpenFile(url);
			}
		}

		public async void OpenFile(FileData fileToOpen)
		{
			var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

			var fileName = Path.Combine(documents, fileToOpen.FileName);

			if (NSFileManager.DefaultManager.FileExists(fileName))
			{
				var url = new NSUrl(fileName, true);

				OpenFile(url);
			}
			else
			{
				await SaveFile(fileToOpen);
				OpenFile(fileToOpen);
			}
		}
	}
}
