using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CWDev.SLNTools.Core;
using log4net;

namespace SolutionPacker
{
    public class Packer
    {

        private static readonly ILog log = LogManager.GetLogger(typeof(Packer));

        private List<string> solutions;
        private string basePath;
        private string solutionName;
        private readonly string solutionsToInclude;

        public string OutputSolutionFileName
        {
            get { return Path.Combine(basePath, solutionName); }
            set
            {
                solutionName = Path.GetFileName(value);
                basePath = Path.GetDirectoryName(value);
            }
        }

        public IEnumerable<string> InputSolutions => solutions;

        internal static void CobbleTogether(PackerArguments arguments)
        {
            new Packer(arguments).Cobble();
        }

        private Packer(PackerArguments arguments)
        {
            basePath = arguments.BasePath;
            solutionName = arguments.SLNName;
            solutionsToInclude = arguments.SolutionsToInclude;
        }

        /// <summary>Initializes a new instance of the <see cref="T:System.Object" /> class.</summary>
        public Packer(string outputSolutionFileName, params string[] fullSolutionFilePathes)
        {
            if (File.Exists(outputSolutionFileName))
                File.Delete(outputSolutionFileName);
            OutputSolutionFileName = outputSolutionFileName;
            solutions = new List<string>(fullSolutionFilePathes);
        }

        public Packer AddSolutions(params string[] fullSolutionFilePathes)
        {
            solutions.AddRange(fullSolutionFilePathes);
            return this;
        }

        public Packer RemoveSolutions(params string[] fullSolutionFilePathes)
        {
            foreach (var filePath in fullSolutionFilePathes)
                solutions.Remove(filePath);
            return this;
        }

        public Task<Packer> ExecuteAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.Run(() =>
            {
                Cobble();
                return this;
            }, cancellationToken);
        }


        private void Cobble()
        {
            var solutionsFiles = DetermineSolutionFiles();
            CreateNewSolution(solutionsFiles.ToArray());
        }

        private void CreateNewSolution(SolutionFile[] solutionFiles)
        {
            var newSolution = new SolutionFile(
                Path.Combine(basePath, solutionName),
                PrepareHeaders(solutionFiles),
                PrepareProjects(solutionFiles),
                PrepareSections(solutionFiles));

            MergeSolutionFiles(newSolution);

            newSolution.Save();
        }

        private IDictionary<Project, HashSet<Project>> BuildProjectDependencygraph(SolutionFile[] solutionFiles)
        {
            log.Info("Building project dependencygraph");
            var allProjects = solutionFiles.SelectMany(sf => sf.Projects)
                .Where(p => p.ProjectTypeGuid != KnownProjectTypeGuid.SolutionFolder)
                .Distinct(new KeyEqualityComparer<Project>(p => p.ProjectGuid))
                .ToArray();

            var dependencies = allProjects.ToDictionary(p => p, p => new HashSet<Project>());
            foreach (Project project in allProjects)
            {
                foreach (string referenceName in CSProjParser.GetReferenceNames(project.FullPath))
                {
                    Project referencedProject = allProjects.FirstOrDefault(p => p.ProjectName.Equals(referenceName));
                    if (referencedProject != null)
                        dependencies[project].Add(referencedProject);
                }
            }
            return dependencies;
        }

        private IEnumerable<SolutionFile> DetermineSolutionFiles()
        {
            if (solutions != null && solutions.Any())
                return solutions.Select(SolutionFile.FromFile);
            string[] includeList = solutionsToInclude.Split(';').Select(s => s.Trim()).ToArray();
            Func<string, bool> includePredicate = s => includeList.Contains(Path.GetFileName(s));
            if (solutionsToInclude.Equals("*"))
                includePredicate = s => true;

            return Directory.EnumerateFiles(basePath, "*.sln", SearchOption.AllDirectories)
                .Where(includePredicate)
                .Select(SolutionFile.FromFile);
        }

        private IEnumerable<Section> PrepareSections(SolutionFile[] solutionFiles)
        {
            log.Info("Adding global sections");
            return solutionFiles.SelectMany(sf => sf.GlobalSections).Distinct(new KeyEqualityComparer<Section>(s => s.Name));
        }

        private IEnumerable<string> PrepareHeaders(SolutionFile[] solutionFiles)
        {
            log.Info("Adding headers");
            return solutionFiles.SelectMany(sf => sf.Headers).Distinct(new KeyEqualityComparer<string>(s => s));
        }

        private IEnumerable<Project> PrepareProjects(SolutionFile[] solutionFiles)
        {
            log.Info("Adding projects");
            IDictionary<Project, HashSet<Project>> dependencies = BuildProjectDependencygraph(solutionFiles);
            foreach (var solutionFile in solutionFiles)
            {
                var projectFolderForSolution = InsetProjectFolderForSolution(solutionFile);
                var directoryAdjustmentPath = Path.GetDirectoryName(solutionFile.SolutionFullPath.Replace(basePath, string.Empty));

                log.Info($"Adding {projectFolderForSolution.ProjectName} ({solutionFile.SolutionFullPath})");
                foreach (var project in solutionFile.Projects.ToArray())
                {
                    AdjustProjectPath(project, directoryAdjustmentPath);
                    InsertProjectDependencies(project, dependencies);
                    if (project.ParentFolder == null && project != projectFolderForSolution)
                        project.ParentFolderGuid = projectFolderForSolution.ProjectGuid;

                    yield return project;
                }
            }
        }

        private void InsertProjectDependencies(Project project, IDictionary<Project, HashSet<Project>> dependencies)
        {
            HashSet<Project> referencedProjects;
            if (dependencies.TryGetValue(project, out referencedProjects))
            {
                var propertyLines = referencedProjects.Select(rp => new PropertyLine(rp.ProjectGuid, rp.ProjectGuid));

                Section section = project.ProjectSections.SingleOrDefault(ps => ps.SectionType == "ProjectSection" && ps.Name == "ProjectDependencies" && ps.Step == "postProject");
                if (section == null)
                    project.ProjectSections.Add(new Section("ProjectDependencies", "ProjectSection", "postProject", propertyLines));
                else
                    section.PropertyLines.AddRange(propertyLines);
            }
        }

        private void MergeSolutionFiles(SolutionFile newSolutionFile)
        {
            log.Info("Merging 'Solution Items' folders");
            var projectFolders = newSolutionFile.Projects.Where(p => p.ProjectName == "Solution Items").ToArray();
            foreach (Project project in projectFolders)
                newSolutionFile.Projects.Remove(project);

            var propertyLines = projectFolders.SelectMany(sipf => sipf.ProjectSections)
                .Where(ps => ps.SectionType == "ProjectSection" && ps.Name == "SolutionItems" && ps.Step == "preProject")
                .SelectMany(ps => ps.PropertyLines)
                .Distinct(new KeyEqualityComparer<PropertyLine>(pl => pl.Name));

            var mergeProjectFolder = InsetProjectFolderForSolution(newSolutionFile, "Solution Items");
            mergeProjectFolder.ProjectSections.Add(new Section("SolutionItems", "ProjectSection", "preProject", propertyLines));

            foreach (var subFolder in projectFolders.SelectMany(p => p.Childs))
                subFolder.ParentFolderGuid = mergeProjectFolder.ProjectGuid;
        }

        private void AdjustProjectPath(Project project, string directoryAdjustmentPath)
        {
            if (project.ProjectTypeGuid != KnownProjectTypeGuid.SolutionFolder)
                project.RelativePath = Path.Combine(directoryAdjustmentPath, project.RelativePath).Remove(0, 1);
            else
            {
                var section = project.ProjectSections.SingleOrDefault(ps => ps.SectionType == "ProjectSection" && ps.Name == "SolutionItems");
                if (section != null)
                {
                    var lines = section.PropertyLines.Select(l => new PropertyLine(Path.Combine(directoryAdjustmentPath, l.Name).Remove(0, 1), Path.Combine(directoryAdjustmentPath, l.Value).Remove(0, 1))).ToArray();
                    section.PropertyLines.Clear();
                    section.PropertyLines.AddRange(lines);
                }
            }
        }

        private Project InsetProjectFolderForSolution(SolutionFile solutionFile, string name = null)
        {
            name = name ?? Path.GetFileNameWithoutExtension(solutionFile.SolutionFullPath)?.Replace('.', '-');
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Could not generate Solutionname");

            var projectGuid = Guid.NewGuid().ToString("B");
            solutionFile.Projects.Add(new Project(solutionFile,
                projectGuid,
                KnownProjectTypeGuid.SolutionFolder,
                name,
                name,
                null,
                Enumerable.Empty<Section>(),
                Enumerable.Empty<PropertyLine>(),
                Enumerable.Empty<PropertyLine>()));

            return solutionFile.Projects.FindByGuid(projectGuid);
        }
    }
}