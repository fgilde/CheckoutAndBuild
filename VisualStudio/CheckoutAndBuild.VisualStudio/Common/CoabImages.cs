using System;
using Microsoft.VisualStudio.Imaging.Interop;

namespace CheckoutAndBuild.VisualStudio
{
	/// <summary>Image monikers registered by Resources/CheckoutAndBuild.imagemanifest.</summary>
	public static class CoabImages
	{
		public static readonly Guid ImageCatalogGuid = new Guid("7c8d9e0f-1a2b-4c3d-9e4f-5a6b7c8d9e0f");

		public static ImageMoniker Icon => new ImageMoniker { Guid = ImageCatalogGuid, Id = 1 };
	}
}
