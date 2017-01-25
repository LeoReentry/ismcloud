namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class SoftwareModelaktualisieren : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Softwares", "Url", c => c.String());
            AddColumn("dbo.Softwares", "Author", c => c.String());
            AddColumn("dbo.Softwares", "Changelog", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Softwares", "Changelog");
            DropColumn("dbo.Softwares", "Author");
            DropColumn("dbo.Softwares", "Url");
        }
    }
}
