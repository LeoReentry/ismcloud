namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Addfieldstosoftwaremodel : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Softwares", "Status", c => c.String());
            AddColumn("dbo.Softwares", "Checksum", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Softwares", "Checksum");
            DropColumn("dbo.Softwares", "Status");
        }
    }
}
