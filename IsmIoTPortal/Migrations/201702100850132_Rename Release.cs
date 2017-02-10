namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RenameRelease : DbMigration
    {
        public override void Up()
        {
            RenameColumn(table: "dbo.NewDevices", name: "SoftwareId", newName: "ReleaseId");
            RenameIndex(table: "dbo.NewDevices", name: "IX_SoftwareId", newName: "IX_ReleaseId");
        }
        
        public override void Down()
        {
            RenameIndex(table: "dbo.NewDevices", name: "IX_ReleaseId", newName: "IX_SoftwareId");
            RenameColumn(table: "dbo.NewDevices", name: "ReleaseId", newName: "SoftwareId");
        }
    }
}
