namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Addpasswordfordeviceusedduringprovisioning : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.NewDevices", "Password", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.NewDevices", "Password");
        }
    }
}
