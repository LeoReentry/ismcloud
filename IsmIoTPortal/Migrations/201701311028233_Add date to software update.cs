namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Adddatetosoftwareupdate : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Softwares", "Date", c => c.DateTime(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Softwares", "Date");
        }
    }
}
