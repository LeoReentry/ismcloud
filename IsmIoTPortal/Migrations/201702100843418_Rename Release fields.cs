namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RenameReleasefields : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.IsmDevices", "SoftwareId", "dbo.Releases");
            DropForeignKey("dbo.NewDevices", "SoftwareId", "dbo.Releases");
            DropPrimaryKey("dbo.Releases");
            AddColumn("dbo.Releases", "ReleaseId", c => c.Int(nullable: false, identity: true));
            AddColumn("dbo.Releases", "Name", c => c.String());
            AddPrimaryKey("dbo.Releases", "ReleaseId");
            AddForeignKey("dbo.IsmDevices", "SoftwareId", "dbo.Releases", "ReleaseId", cascadeDelete: true);
            AddForeignKey("dbo.NewDevices", "SoftwareId", "dbo.Releases", "ReleaseId", cascadeDelete: true);
            DropColumn("dbo.Releases", "SoftwareId");
            DropColumn("dbo.Releases", "SoftwareVersion");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Releases", "SoftwareVersion", c => c.String());
            AddColumn("dbo.Releases", "SoftwareId", c => c.Int(nullable: false, identity: true));
            DropForeignKey("dbo.NewDevices", "SoftwareId", "dbo.Releases");
            DropForeignKey("dbo.IsmDevices", "SoftwareId", "dbo.Releases");
            DropPrimaryKey("dbo.Releases");
            DropColumn("dbo.Releases", "Name");
            DropColumn("dbo.Releases", "ReleaseId");
            AddPrimaryKey("dbo.Releases", "SoftwareId");
            AddForeignKey("dbo.NewDevices", "SoftwareId", "dbo.Releases", "SoftwareId", cascadeDelete: true);
            AddForeignKey("dbo.IsmDevices", "SoftwareId", "dbo.Releases", "SoftwareId", cascadeDelete: true);
        }
    }
}
