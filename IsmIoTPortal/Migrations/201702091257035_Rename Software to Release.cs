namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RenameSoftwaretoRelease : DbMigration
    {
        public override void Up()
        {
            RenameTable(name: "dbo.Softwares", newName: "Releases");
        }
        
        public override void Down()
        {
            RenameTable(name: "dbo.Releases", newName: "Softwares");
        }
    }
}
