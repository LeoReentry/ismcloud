namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class ChangeSoftwareVersionModel : DbMigration
    {
        public override void Up()
        {
            DropColumn("dbo.SoftwareVersions", "MajorVersion");
            DropColumn("dbo.SoftwareVersions", "MinorVersion");
            DropColumn("dbo.SoftwareVersions", "PatchVersion");
        }
        
        public override void Down()
        {
            AddColumn("dbo.SoftwareVersions", "PatchVersion", c => c.Int(nullable: false));
            AddColumn("dbo.SoftwareVersions", "MinorVersion", c => c.Int(nullable: false));
            AddColumn("dbo.SoftwareVersions", "MajorVersion", c => c.Int(nullable: false));
        }
    }
}
