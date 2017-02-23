namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RemoveLogmessagestringfromDeviceModel : DbMigration
    {
        public override void Up()
        {
            DropColumn("dbo.IsmDevices", "UpdateLog");
        }
        
        public override void Down()
        {
            AddColumn("dbo.IsmDevices", "UpdateLog", c => c.String());
        }
    }
}
