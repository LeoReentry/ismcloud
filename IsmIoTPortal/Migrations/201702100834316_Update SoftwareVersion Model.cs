namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class UpdateSoftwareVersionModel : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.SoftwareVersions", "MajorVersion", c => c.Int(nullable: false));
            AddColumn("dbo.SoftwareVersions", "MinorVersion", c => c.Int(nullable: false));
            AddColumn("dbo.SoftwareVersions", "PatchVersion", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.SoftwareVersions", "PatchVersion");
            DropColumn("dbo.SoftwareVersions", "MinorVersion");
            DropColumn("dbo.SoftwareVersions", "MajorVersion");
        }
    }
}
