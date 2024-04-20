using EmpireCompiler.Core.Empire;
using EmpireCompiler.Models;
using EmpireCompiler.Models.Grunts;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EmpireCompiler.Core
{
    public interface IReferenceAssemblyService
    {
        Task<IEnumerable<ReferenceAssembly>> GetReferenceAssemblies();
        Task<IEnumerable<ReferenceAssembly>> GetDefaultNet35ReferenceAssemblies();
        Task<IEnumerable<ReferenceAssembly>> GetDefaultNet40ReferenceAssemblies();
        Task<IEnumerable<ReferenceAssembly>> GetDefaultNet45ReferenceAssemblies();
        Task<ReferenceAssembly> GetReferenceAssembly(int id);
        Task<ReferenceAssembly> GetReferenceAssemblyByName(string name, Common.DotNetVersion version);
        Task<ReferenceAssembly> CreateReferenceAssembly(ReferenceAssembly assembly);
        Task<IEnumerable<ReferenceAssembly>> CreateReferenceAssemblies(params ReferenceAssembly[] assemblies);
        Task<ReferenceAssembly> EditReferenceAssembly(ReferenceAssembly assembly);
        Task DeleteReferenceAssembly(int id);
    }

    public interface IEmbeddedResourceService
    {
        Task<IEnumerable<EmbeddedResource>> GetEmbeddedResources();
        Task<EmbeddedResource> GetEmbeddedResource(int id);
        Task<EmbeddedResource> GetEmbeddedResourceByName(string name);
        Task<EmbeddedResource> CreateEmbeddedResource(EmbeddedResource resource);
        Task<IEnumerable<EmbeddedResource>> CreateEmbeddedResources(params EmbeddedResource[] resources);
        Task<EmbeddedResource> EditEmbeddedResource(EmbeddedResource resource);
        Task DeleteEmbeddedResource(int id);
    }

    public interface IReferenceSourceLibraryService
    {
        Task<IEnumerable<ReferenceSourceLibrary>> GetReferenceSourceLibraries();
        Task<ReferenceSourceLibrary> GetReferenceSourceLibrary(int id);
        Task<ReferenceSourceLibrary> GetReferenceSourceLibraryByName(string name);
        Task<ReferenceSourceLibrary> CreateReferenceSourceLibrary(ReferenceSourceLibrary library);
        Task<IEnumerable<ReferenceSourceLibrary>> CreateReferenceSourceLibraries(params ReferenceSourceLibrary[] libraries);
        Task<ReferenceSourceLibrary> EditReferenceSourceLibrary(ReferenceSourceLibrary library);
        Task DeleteReferenceSourceLibrary(int id);
    }

    public interface IGruntTaskOptionService
    {
        Task<TaskOption> EditGruntTaskOption(TaskOption option);
        Task<TaskOption> CreateGruntTaskOption(TaskOption option);
        Task<IEnumerable<TaskOption>> CreateGruntTaskOptions(params TaskOption[] options);
    }


    public interface IGruntTaskService : IReferenceAssemblyService, IEmbeddedResourceService, IReferenceSourceLibraryService,
        IGruntTaskOptionService
    {
        Task<IEnumerable<GruntTask>> GetGruntTasks();
        Task<IEnumerable<GruntTask>> GetGruntTasksForGrunt(int gruntId);
        Task<GruntTask> GetGruntTask(int id);
        Task<GruntTask> GetGruntTaskByName(string name, Common.DotNetVersion version = Common.DotNetVersion.Net35);
        Task<GruntTask> CreateGruntTask(GruntTask task);
        Task<IEnumerable<GruntTask>> CreateGruntTasks(params GruntTask[] tasks);
        Task<GruntTask> EditGruntTask(GruntTask task);
        Task DeleteGruntTask(int taskId);
        Task<string> ParseParametersIntoTask(GruntTask task, List<ParsedParameter> parameters);
    }

    public interface ICovenantService2 : IGruntTaskService
    {
        Task<IEnumerable<T>> CreateEntities<T>(params T[] entities);
        EmpireContext GetEmpire();
        void DisposeContext();
    }

    public class EmpireService : ICovenantService2
    {
        protected EmpireContext _context;
        public EmpireService()
        {
            _context = new EmpireContext();
        }

        public EmpireContext GetEmpire()
        {
            return _context;
        }

        //Eventually this may access the Empire DB instead but for now just shove it in a list 
        //This actually might end up being handled on the python side anyways
        public async Task<IEnumerable<T>> CreateEntities<T>(params T[] entities)
        {
            foreach (T entity in entities)
            {
                _context.Add(entity);
            }
            return entities;
        }

        public async Task<string> ParseParametersIntoTask(GruntTask task, List<ParsedParameter> parameters)
        {
            return null;
        }

        #region Core functions for Empire Service
        public async Task<IEnumerable<EmbeddedResource>> CreateEmbeddedResources(params EmbeddedResource[] resources)
        {
            _context.embeddedResources = resources.OfType<EmbeddedResource>().ToList();
            return resources;
        }

        //Reference Assembly methods
        public async Task<ReferenceAssembly> GetReferenceAssemblyByName(string name, Common.DotNetVersion version)
        {
            ReferenceAssembly assembly = _context.referenceAssemblies.First(asm => asm.Name == name);
            return assembly;
        }

        public async Task<IEnumerable<ReferenceAssembly>> CreateReferenceAssemblies(params ReferenceAssembly[] assemblies)
        {
            _context.referenceAssemblies = assemblies.OfType<ReferenceAssembly>().ToList();
            return assemblies;
        }
        //Refrence library methods
        public async Task<IEnumerable<ReferenceSourceLibrary>> CreateReferenceSourceLibraries(params ReferenceSourceLibrary[] libraries)
        {
            _context.referenceSourceLibraries = libraries.OfType<ReferenceSourceLibrary>().ToList();
            return libraries;
        }

        public async Task<ReferenceSourceLibrary> GetReferenceSourceLibraryByName(string name)
        {
            //original had an include for ReferenceSourceLibraryReferenceAssemblies.ReferenceAssembly and ReferenceSourceLibraryEmbeddedResources.EmbeddedResource
            // if there is an issue with retrieving a library look into this atrribute more 
            ReferenceSourceLibrary library = _context.referenceSourceLibraries.First(RSL => RSL.Name == name);
            if (library == null)
            {
                Console.WriteLine($"NotFound - ReferenceSourceLibrary with Name: {name}");
            }
            return library;
        }

        //Grunt Task Methods
        public async Task<GruntTask> GetGruntTask(int id)
        {
            GruntTask task = _context.gruntTasks.FirstOrDefault(tsk => tsk.Id == id);
            if (task == null)
            {
                Console.WriteLine($"NotFound - GruntTask with id: {id}");
            }
            return task;
        }

        public async Task<GruntTask> CreateGruntTask(GruntTask task)
        {
            //Need to consider restructuring this method and the context class 
            //The way it is currently done is built around interacting with a sqllite db. 
            //need to decide if the Empire server will manage the DB directly.
            List<TaskOption> options = task.Options.ToList();
            List<EmbeddedResource> resources = task.EmbeddedResources.ToList();
            List<ReferenceAssembly> assemblies = task.ReferenceAssemblies.ToList();
            List<ReferenceSourceLibrary> libraries = task.ReferenceSourceLibraries.ToList();
            task.Options = new List<TaskOption>();
            task.EmbeddedResources.ForEach(ER => task.Remove(ER));
            task.ReferenceAssemblies.ForEach(RA => task.Remove(RA));
            task.ReferenceSourceLibraries.ForEach(RSL => task.Remove(RSL));
            task.Id = _context.GetNextTaskID();

            foreach (TaskOption option in options)
            {
                option.GruntTaskId = task.Id;
                //since the option is being added to the task not sure the options need to be stored separately 
                //this was a structure done for the covenant Db
                _context.Add(option);
                task.Options.Add(option);
            }
            foreach (EmbeddedResource resource in resources)
            {
                await this.CreateEntities(
                    new GruntTaskEmbeddedResource
                    {
                        EmbeddedResource = await this.GetEmbeddedResourceByName(resource.Name),
                        GruntTask = task
                    }
                );
                task.Add(resource);
            }
            foreach (ReferenceAssembly assembly in assemblies)
            {
                //This is all Database schema based so doesn't work without the databasse
                await this.CreateEntities(
                    new GruntTaskReferenceAssembly
                    {
                        ReferenceAssembly = await this.GetReferenceAssemblyByName(assembly.Name, assembly.DotNetVersion),
                        GruntTask = task
                    }
                );
                //instead do this
                task.Add(assembly);
            }
            foreach (ReferenceSourceLibrary library in libraries)
            {
                task.Add(library);
            }
            //add the Grunt task to teh context list
            _context.Add(task);
            return await this.GetGruntTask(task.Id);
        }

        public async Task<IEnumerable<GruntTask>> GetGruntTasks()
        {
            return _context.gruntTasks;
        }

        public void DisposeContext()
        {
            _context = new EmpireContext();
        }
        #endregion

        #region GruntTaskComponents EmbeddedResource Actions
        public async Task<IEnumerable<EmbeddedResource>> GetEmbeddedResources()
        {
            return _context.embeddedResources.ToList();
        }

        public async Task<EmbeddedResource> GetEmbeddedResource(int id)
        {
            EmbeddedResource resource = _context.embeddedResources.FirstOrDefault(ER => ER.Id == id);
            if (resource == null)
            {
                throw new ControllerNotFoundException($"NotFound - EmbeddedResource with id: {id}");
            }
            return resource;
        }

        public async Task<EmbeddedResource> GetEmbeddedResourceByName(string name)
        {
            EmbeddedResource resource = _context.embeddedResources
                .Where(ER => ER.Name == name)
                .FirstOrDefault();
            if (resource == null)
            {
                throw new ControllerNotFoundException($"NotFound - EmbeddedResource with Name: {name}");
            }
            return resource;
        }

        public async Task<EmbeddedResource> CreateEmbeddedResource(EmbeddedResource resource)
        {
            _context.embeddedResources.Add(resource);
            return await this.GetEmbeddedResource(resource.Id);
        }

        //NOT FULLY IMPLEMENTED
        public async Task<EmbeddedResource> EditEmbeddedResource(EmbeddedResource resource)
        {
            EmbeddedResource matchingResource = await this.GetEmbeddedResource(resource.Id);
            matchingResource.Name = resource.Name;
            matchingResource.Location = resource.Location;
            return await this.GetEmbeddedResource(matchingResource.Id);
        }

        public async Task DeleteEmbeddedResource(int id)
        {
            EmbeddedResource matchingResource = await this.GetEmbeddedResource(id);
            _context.embeddedResources.Remove(matchingResource);
        }
        #endregion

        #region GruntTaskOption Actions
        public async Task<TaskOption> EditGruntTaskOption(TaskOption option)
        {
            return option;
        }

        public async Task<TaskOption> CreateGruntTaskOption(TaskOption option)
        {
            _context.Add(option);
            return option;
        }

        public async Task<IEnumerable<TaskOption>> CreateGruntTaskOptions(params TaskOption[] options)
        {
            _context.gruntTaskOptions.AddRange(options);

            return options;
        }
        #endregion

        #region GruntTask Actions

        public async Task<IEnumerable<GruntTask>> GetGruntTasksForGrunt(int gruntId)
        {
            return _context.gruntTasks
                .AsEnumerable()
                .Where(T => T.CompatibleDotNetVersions.Contains(Common.DotNetVersion.Net35));
        }

        public async Task<GruntTask> GetGruntTaskByName(string name, Common.DotNetVersion version = Common.DotNetVersion.Net35)
        {
            string lower = name.ToLower();

            GruntTask task = _context.gruntTasks
                .Where(T => T.Name.ToLower() == lower)
                .AsEnumerable()
                .Where(T => T.CompatibleDotNetVersions.Contains(version))
                .FirstOrDefault();
            if (task == null)
            {
                // Probably bad performance here
                task = _context.gruntTasks
                    .AsEnumerable()
                    .Where(T => T.Aliases.Any(A => A.Equals(lower, StringComparison.CurrentCultureIgnoreCase)))
                    .Where(T => T.CompatibleDotNetVersions.Contains(version))
                    .FirstOrDefault();
                if (task == null)
                {
                    throw new ControllerNotFoundException($"NotFound - GruntTask with Name: {name}");
                }
            }
            return await Task.FromResult(task);
        }

        public async Task<IEnumerable<GruntTask>> CreateGruntTasks(params GruntTask[] tasks)
        {
            List<GruntTask> createdTasks = new List<GruntTask>();
            foreach (GruntTask t in tasks)
            {
                createdTasks.Add(await this.CreateGruntTask(t));
            }
            return createdTasks;
        }

        public async Task<GruntTask> EditGruntTask(GruntTask task)
        {
            return null;
        }

        public async Task DeleteGruntTask(int taskId)
        {

        }
        #endregion

        #region GruntTaskComponents ReferenceSourceLibrary Actions
        public async Task<IEnumerable<ReferenceSourceLibrary>> GetReferenceSourceLibraries()
        {
            return _context.referenceSourceLibraries;
        }

        public async Task<ReferenceSourceLibrary> GetReferenceSourceLibrary(int id)
        {
            ReferenceSourceLibrary library = _context.referenceSourceLibraries
                .Where(RSL => RSL.Id == id)
                .FirstOrDefault();
            if (library == null)
            {
                throw new ControllerNotFoundException($"NotFound - ReferenceSourceLibrary with id: {id}");
            }
            return library;
        }

        public async Task<ReferenceSourceLibrary> CreateReferenceSourceLibrary(ReferenceSourceLibrary library)
        {
            _context.referenceSourceLibraries.Add(library);
            return await this.GetReferenceSourceLibrary(library.Id);
        }

        //This not properly down at the moment
        public async Task<ReferenceSourceLibrary> EditReferenceSourceLibrary(ReferenceSourceLibrary library)
        {
            return await this.GetReferenceSourceLibrary(library.Id);
        }

        public async Task DeleteReferenceSourceLibrary(int id)
        {
            ReferenceSourceLibrary referenceSourceLibrary = await this.GetReferenceSourceLibrary(id);
            _context.referenceSourceLibraries.Remove(referenceSourceLibrary);

        }
        #endregion
        #region GruntTaskComponent ReferenceAssembly Actions
        public async Task<IEnumerable<ReferenceAssembly>> GetReferenceAssemblies()
        {
            return _context.referenceAssemblies.ToList();
        }

        public async Task<IEnumerable<ReferenceAssembly>> GetDefaultNet35ReferenceAssemblies()
        {
            return new List<ReferenceAssembly>
            {
                await this.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net35),
                await this.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net35),
                await this.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net35)
            };
        }

        public async Task<IEnumerable<ReferenceAssembly>> GetDefaultNet40ReferenceAssemblies()
        {
            return new List<ReferenceAssembly>
            {
                await this.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net40),
                await this.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net40),
                await this.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net40)
            };
        }
        public async Task<IEnumerable<ReferenceAssembly>> GetDefaultNet45ReferenceAssemblies()
        {
            return new List<ReferenceAssembly>
            {
                await this.GetReferenceAssemblyByName("mscorlib.dll", Common.DotNetVersion.Net45),
                await this.GetReferenceAssemblyByName("System.dll", Common.DotNetVersion.Net45),
                await this.GetReferenceAssemblyByName("System.Core.dll", Common.DotNetVersion.Net45)
            };
        }
        public async Task<ReferenceAssembly> GetReferenceAssembly(int id)
        {
            ReferenceAssembly assembly = _context.referenceAssemblies.FirstOrDefault(RA => RA.Id == id);
            if (assembly == null)
            {
                throw new ControllerNotFoundException($"NotFound - ReferenceAssembly with id: {id}");
            }
            return assembly;
        }

        public async Task<ReferenceAssembly> CreateReferenceAssembly(ReferenceAssembly assembly)
        {
            _context.referenceAssemblies.Add(assembly);
            return await this.GetReferenceAssembly(assembly.Id);
        }

        public async Task<ReferenceAssembly> EditReferenceAssembly(ReferenceAssembly assembly)
        {
            ReferenceAssembly matchingAssembly = await this.GetReferenceAssembly(assembly.Id);
            matchingAssembly.Name = assembly.Name;
            matchingAssembly.Location = assembly.Location;
            matchingAssembly.DotNetVersion = assembly.DotNetVersion;
            return await this.GetReferenceAssembly(matchingAssembly.Id);
        }

        public async Task DeleteReferenceAssembly(int id)
        {
            ReferenceAssembly matchingAssembly = await this.GetReferenceAssembly(id);
            _context.referenceAssemblies.Remove(matchingAssembly);
        }
        #endregion
    }

}