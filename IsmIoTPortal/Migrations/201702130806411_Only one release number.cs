namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Onlyonereleasenumber : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Releases", "Num", c => c.Int(nullable: false));
            AddColumn("dbo.SoftwareVersions", "CurrentReleaseNum", c => c.Int(nullable: false));
            DropColumn("dbo.SoftwareVersions", "InternalReleaseNum");
        }
        
        public override void Down()
        {
            AddColumn("dbo.SoftwareVersions", "InternalReleaseNum", c => c.String());
            DropColumn("dbo.SoftwareVersions", "CurrentReleaseNum");
            DropColumn("dbo.Releases", "Num");
        }
    }
}
