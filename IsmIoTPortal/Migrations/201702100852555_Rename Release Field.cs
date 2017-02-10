namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RenameReleaseField : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Releases", "Name", c => c.String());
            DropColumn("dbo.Releases", "SoftwareVersion");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Releases", "SoftwareVersion", c => c.String());
            DropColumn("dbo.Releases", "Name");
        }
    }
}
