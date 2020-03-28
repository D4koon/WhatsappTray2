using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using WhatsappTray2.Properties;

namespace WhatsappTray2
{
	public static class IconDrawing
	{
		public static Icon waIcon;

		/// <summary>
		/// When working with bitmaps always check if everything is properly freed.
		/// To check open taskmanager->Select row->GDI-Objects
		/// </summary>
		/// <param name="text"></param>
		/// <returns></returns>

		public static Bitmap DrawBitmap(string text)
		{
			var test = GetResourceStream("WhatsAppTrayNew2.png");

			Bitmap bitmap = new Bitmap(test);

			// Create brush.
			SolidBrush drawBrush = new SolidBrush(Color.FromArgb(0x00, 0x00, 0xC6));

			using (Graphics gr = Graphics.FromImage(bitmap)) {
				gr.SmoothingMode = SmoothingMode.AntiAlias;
				//gr.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

				//if (text.Length == 1) {
				//	Font drawFont = new Font("Arial", 19, FontStyle.Bold);
				//	gr.DrawString(text, drawFont, drawBrush, 4, 2);
				//} else {
				//	Font drawFont = new Font("Arial", 14, FontStyle.Bold);
				//	gr.DrawString(text, drawFont, drawBrush, 2, 5);
				//}

				if (text.Length == 1) {
					Font drawFont = new Font("Arial", 150, FontStyle.Bold);
					gr.DrawString(text, drawFont, drawBrush, 35, 15);
				} else {
					Font drawFont = new Font("Arial", 115, FontStyle.Bold);
					gr.DrawString(text, drawFont, drawBrush, 10, 40);
				}

				gr.Flush();
			}

			// Making it smaller make it less pixelated.
			// It seems like the windows-automatic scaling is not really good
			bitmap = ResizeImage(bitmap, 32, 32);

			return bitmap;
		}

		/// <summary>
		/// Resize the image to the specified width and height.
		/// </summary>
		/// <param name="image">The image to resize.</param>
		/// <param name="width">The width to resize to.</param>
		/// <param name="height">The height to resize to.</param>
		/// <returns>The resized image.</returns>
		public static Bitmap ResizeImage(Image image, int width, int height)
		{
			var destRect = new Rectangle(0, 0, width, height);
			var destImage = new Bitmap(width, height);

			destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

			using (var graphics = Graphics.FromImage(destImage)) {
				graphics.CompositingMode = CompositingMode.SourceCopy;
				graphics.CompositingQuality = CompositingQuality.HighQuality;
				graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
				graphics.SmoothingMode = SmoothingMode.HighQuality;
				graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

				using (var wrapMode = new System.Drawing.Imaging.ImageAttributes()) {
					wrapMode.SetWrapMode(WrapMode.TileFlipXY);
					graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
				}
			}

			return destImage;
		}

		public static Icon DrawIcon(string text)
		{
			//waIcon = Icon.ExtractAssociatedIcon("C:/project/WhatsappTray2/WhatsappTray2/images/WhatsappTray4.ico");

			//Bitmap bitmap = waIcon.ToBitmap();
			//waIcon.Dispose();

			var bitmap = DrawBitmap(text);

			var icon = BitmapToIcon(bitmap);

			return icon;
		}

		/// <summary>
		/// Create Icon that calls Destroy-Icon after it goes out of scope.
		/// Unfortunatly Icon.FromHandle() initializes with the internal Icon-constructor Icon(handle, false), which sets the internal value "ownHandle" to false
		/// This way because of the false, DestroyIcon() is not called as can be seen here:
		/// https://referencesource.microsoft.com/#System.Drawing/commonui/System/Drawing/Icon.cs,f2697049dea34e7c,references
		/// To get arround this we get the constructor internal Icon(IntPtr handle, bool takeOwnership) from Icon through reflection and initialize that way
		/// </summary>
		private static Icon BitmapToIcon(Bitmap bitmap)
		{
			Type[] cargt = new[] { typeof(IntPtr), typeof(bool) };
			ConstructorInfo ci = typeof(Icon).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, cargt, null);
			object[] cargs = new[] { (object)bitmap.GetHicon(), true };
			Icon icon = (Icon)ci.Invoke(cargs);
			return icon;
		}

		public static System.Windows.Media.ImageSource ToImageSource(this Icon icon)
		{
			Bitmap bitmap = icon.ToBitmap();
			IntPtr hBitmap = bitmap.GetHbitmap();

			System.Windows.Media.ImageSource wpfBitmap = Imaging.CreateBitmapSourceFromHBitmap(
				hBitmap,
				IntPtr.Zero,
				System.Windows.Int32Rect.Empty,
				BitmapSizeOptions.FromEmptyOptions());

			if (!DeleteObject(hBitmap)) {
				throw new Win32Exception();
			}

			bitmap.Dispose();

			return wpfBitmap;
		}

		public static System.Windows.Media.ImageSource ToImageSource(this Bitmap bitmap)
		{
			IntPtr hBitmap = bitmap.GetHbitmap();

			System.Windows.Media.ImageSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
				hBitmap,
				IntPtr.Zero,
				System.Windows.Int32Rect.Empty,
				BitmapSizeOptions.FromEmptyOptions());

			if (!DeleteObject(hBitmap)) {
				throw new Win32Exception();
			}

			bitmapSource.Freeze();

			return bitmapSource;
		}

		[DllImport("gdi32.dll", SetLastError = true)]
		private static extern bool DeleteObject(IntPtr hObject);

		public static Stream GetResourceStream(string searchedName)
		{
			var asm = Assembly.GetEntryAssembly();
			var names = asm.GetManifestResourceNames();

			string foundName = null;
			foreach (var name in names) {
				if (name.Contains(searchedName)) {
					foundName = name;
				}
			}

			if (foundName == null) {
				return null;
			}

			return asm.GetManifestResourceStream(foundName);
		}

		public static string[] GetResourceNames()
		{
			var asm = Assembly.GetEntryAssembly();
			string resName = asm.GetName().Name + ".g.resources";
			using (var stream = asm.GetManifestResourceStream(resName))
			using (var reader = new System.Resources.ResourceReader(stream)) {
				return reader.Cast<System.Collections.DictionaryEntry>().Select(entry => (string)entry.Key).ToArray();
			}
		}

		public static byte[] GetResource(string name)
		{
			var asm = Assembly.GetEntryAssembly();
			string resName = asm.GetName().Name + ".g.resources";
			using (var stream = asm.GetManifestResourceStream(resName))
			using (var reader = new System.Resources.ResourceReader(stream)) {
				byte[] buffer;
				string type = "System.Drawing.Bitmap";
				reader.GetResourceData(name, out type, out buffer);
				return buffer;
			}
		}
	}
}
