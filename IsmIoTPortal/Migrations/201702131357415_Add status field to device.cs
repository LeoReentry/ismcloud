namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Addstatusfieldtodevice : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.IsmDevices", "UpdateStatus", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.IsmDevices", "UpdateStatus");
        }
    }
}
