namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class NewDevices : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.NewDevices",
                c => new
                    {
                        IsmDeviceId = c.Int(nullable: false, identity: true),
                        DeviceId = c.String(),
                        LocationId = c.Int(nullable: false),
                        SoftwareId = c.Int(nullable: false),
                        HardwareId = c.Int(nullable: false),
                        Code = c.String(),
                    })
                .PrimaryKey(t => t.IsmDeviceId)
                .ForeignKey("dbo.Hardwares", t => t.HardwareId, cascadeDelete: true)
                .ForeignKey("dbo.Locations", t => t.LocationId, cascadeDelete: true)
                .ForeignKey("dbo.Softwares", t => t.SoftwareId, cascadeDelete: true)
                .Index(t => t.LocationId)
                .Index(t => t.SoftwareId)
                .Index(t => t.HardwareId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.NewDevices", "SoftwareId", "dbo.Softwares");
            DropForeignKey("dbo.NewDevices", "LocationId", "dbo.Locations");
            DropForeignKey("dbo.NewDevices", "HardwareId", "dbo.Hardwares");
            DropIndex("dbo.NewDevices", new[] { "HardwareId" });
            DropIndex("dbo.NewDevices", new[] { "SoftwareId" });
            DropIndex("dbo.NewDevices", new[] { "LocationId" });
            DropTable("dbo.NewDevices");
        }
    }
}
