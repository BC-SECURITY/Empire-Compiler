// Author: Jake Krasnov (@_hubbl3)
// Project: Empire (https://github.com/BC-SECURITY/Empire)
// License: GNU GPLv3

using EmpireCompiler.Models.Agents;
using System;
using System.Collections.Generic;

namespace EmpireCompiler.Core.Empire
{
    public class EmpireContext
    {
        // Covenant builds a DB to handle this however Empire is using the compiler as semi stateless 
        // This is not persistent so will be lost on restart 
        public List<Object> gList { get; set; }
        public List<ReferenceAssembly> referenceAssemblies { get; set; }
        public List<EmbeddedResource> embeddedResources { get; set; }
        public List<ReferenceSourceLibrary> referenceSourceLibraries { get; set; }
        public List<ReferenceSourceLibraryReferenceAssembly> referenceSourceLibraryReferenceAssemblies { get; set; }

        public List<TaskOption> gruntTaskOptions { get; set; }

        public List<AgentTask> gruntTasks { get; set; }

        private int nextTaskId;
        public EmpireContext()
        {
            gList = new List<object>();
            referenceAssemblies = new List<ReferenceAssembly>();
            embeddedResources = new List<EmbeddedResource>();
            referenceSourceLibraries = new List<ReferenceSourceLibrary>();
            referenceSourceLibraryReferenceAssemblies = new List<ReferenceSourceLibraryReferenceAssembly>();
            gruntTaskOptions = new List<TaskOption>();
            gruntTasks = new List<AgentTask>();
            nextTaskId = 0;

        }

        //doing this with a DB would have been a lot easier
        //This is probably the wrong way to do this but it appears to be working 
        public void Add(Object entity)
        {
            gList.Add(entity);
        }

        public void Add(AgentTask entity)
        {
            gruntTasks.Add(entity);
        }
        //Not sure why the grunt options are added separately instead of a member of the Grunt Task class
        public void Add(TaskOption entity)
        {
            gruntTaskOptions.Add(entity);
        }

        //generate the next task ID when a task is created
        public int GetNextTaskID()
        {
            this.nextTaskId += 1;
            return this.nextTaskId;
        }
    }
}