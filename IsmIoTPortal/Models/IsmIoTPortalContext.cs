using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace IsmIoTPortal.Models
{
    public class IsmIoTPortalContext : DbContext
    {
        public DbSet<IsmDevice> IsmDevices { get; set; }
        public DbSet<NewDevice> NewDevices { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<Release> Releases { get; set; }
        public DbSet<SoftwareVersion> SoftwareVersions { get; set; }
        public DbSet<Hardware> Hardware { get; set; }
        public DbSet<Command> Commands { get; set; }
        public DbSet<FilamentData> FilamentData { get; set; }
        public DbSet<ImagingProcessorWorkerInstance> ImagingProcessorWorkerInstances { get; set; }
        public DbSet<UpdateLog> UpdateLogs { get; set; }
    }
}