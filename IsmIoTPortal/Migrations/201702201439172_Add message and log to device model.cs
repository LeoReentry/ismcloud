namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Addmessageandlogtodevicemodel : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.IsmDevices", "UpdateMessage", c => c.String());
            AddColumn("dbo.IsmDevices", "UpdateLog", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.IsmDevices", "UpdateLog");
            DropColumn("dbo.IsmDevices", "UpdateMessage");
        }
    }
}
