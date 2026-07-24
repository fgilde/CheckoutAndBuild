using System;
using System.IO;
using Microsoft.VisualStudio.Shell.Interop;
using IServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace FG.CheckoutAndBuild2.Types
{
	public class SimpleVsHierarchy : IVsHierarchy
	{
		
		public string ProjectFile { get; private set; }
		//public Project Project { get; private set; }

		public string ProjectFileName { get { return Path.GetFileName(ProjectFile); } }

		/// <summary>
		/// Initializes a new instance of the <see cref="T:System.Object"/> class.
		/// </summary>
		public SimpleVsHierarchy(string projectFile)
		{
			ProjectFile = projectFile;
			//Project = new Project(projectFile);
		}

		/// <summary>
		/// Returns a string that represents the current object.
		/// </summary>
		/// <returns>
		/// A string that represents the current object.
		/// </returns>
		public override string ToString()
		{
			return ProjectFileName;
		}

		public int GetProperty(uint itemid, int propid, out object pvar)
		{
			pvar = Path.GetFileName(ProjectFile);
			return 0;
		}

		#region Empty Implementations

		public int SetSite(IServiceProvider psp)
		{
			return 0;
		}

		public int GetSite(out IServiceProvider ppSP)
		{
			ppSP = null;
			return 0;
		}

		public int QueryClose(out int pfCanClose)
		{
			pfCanClose = 1;
			return 0;
		}

		public int Close()
		{
			return 0;
		}

		public int GetGuidProperty(uint itemid, int propid, out Guid pguid)
		{
			pguid = Guid.Empty;
			return 0;
		}


		public int SetGuidProperty(uint itemid, int propid, ref Guid rguid)
		{
			return 0;
		}


		public int SetProperty(uint itemid, int propid, object var)
		{
			return 0;
		}

		public int GetNestedHierarchy(uint itemid, ref Guid iidHierarchyNested, out IntPtr ppHierarchyNested,
			out uint pitemidNested)
		{
			ppHierarchyNested = IntPtr.Zero;
			pitemidNested = 0;
			return 0;
		}

		public int GetCanonicalName(uint itemid, out string pbstrName)
		{
			pbstrName = null;
			return 0;
		}

		public int ParseCanonicalName(string pszName, out uint pitemid)
		{
			pitemid = 0;
			return 0;
		}

		public int Unused0()
		{
			return 0;
		}

		public int AdviseHierarchyEvents(IVsHierarchyEvents pEventSink, out uint pdwCookie)
		{
			pdwCookie = 0;
			return 0;
		}

		public int UnadviseHierarchyEvents(uint dwCookie)
		{
			return 0;
		}

		public int Unused1()
		{
			return 0;
		}

		public int Unused2()
		{
			return 0;
		}


		public int Unused3()
		{
			return 0;
		}

		public int Unused4()
		{
			return 0;
		}

		#endregion

	}
}