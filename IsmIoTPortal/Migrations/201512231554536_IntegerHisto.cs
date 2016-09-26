namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class IntegerHisto : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Commands",
                c => new
                    {
                        CommandId = c.Int(nullable: false, identity: true),
                        Cmd = c.String(),
                        Timestamp = c.DateTime(nullable: false),
                        CommandStatus = c.String(),
                        IsmDeviceId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.CommandId)
                .ForeignKey("dbo.IsmDevices", t => t.IsmDeviceId, cascadeDelete: true)
                .Index(t => t.IsmDeviceId);
            
            CreateTable(
                "dbo.IsmDevices",
                c => new
                    {
                        IsmDeviceId = c.Int(nullable: false, identity: true),
                        DeviceId = c.String(),
                        LocationId = c.Int(nullable: false),
                        SoftwareId = c.Int(nullable: false),
                        HardwareId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.IsmDeviceId)
                .ForeignKey("dbo.Hardwares", t => t.HardwareId, cascadeDelete: true)
                .ForeignKey("dbo.Locations", t => t.LocationId, cascadeDelete: true)
                .ForeignKey("dbo.Softwares", t => t.SoftwareId, cascadeDelete: true)
                .Index(t => t.LocationId)
                .Index(t => t.SoftwareId)
                .Index(t => t.HardwareId);
            
            CreateTable(
                "dbo.Hardwares",
                c => new
                    {
                        HardwareId = c.Int(nullable: false, identity: true),
                        Board = c.String(),
                        Camera = c.String(),
                    })
                .PrimaryKey(t => t.HardwareId);
            
            CreateTable(
                "dbo.Locations",
                c => new
                    {
                        LocationId = c.Int(nullable: false, identity: true),
                        Country = c.String(),
                        City = c.String(),
                        PostalCode = c.String(),
                        Street = c.String(),
                        StreetNumber = c.String(),
                        ContactId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.LocationId)
                .ForeignKey("dbo.Contacts", t => t.ContactId, cascadeDelete: true)
                .Index(t => t.ContactId);
            
            CreateTable(
                "dbo.Contacts",
                c => new
                    {
                        ContactId = c.Int(nullable: false, identity: true),
                        FirstName = c.String(),
                        LastName = c.String(),
                        EmailAddress = c.String(),
                        PhoneNumber = c.String(),
                    })
                .PrimaryKey(t => t.ContactId);
            
            CreateTable(
                "dbo.Softwares",
                c => new
                    {
                        SoftwareId = c.Int(nullable: false, identity: true),
                        SoftwareVersion = c.String(),
                    })
                .PrimaryKey(t => t.SoftwareId);
            
            CreateTable(
                "dbo.FilamentDatas",
                c => new
                    {
                        FilamentDataId = c.Int(nullable: false, identity: true),
                        Time = c.DateTime(),
                        FC = c.Double(),
                        FL = c.Double(),
                        H1 = c.Int(),
                        H2 = c.Int(),
                        H3 = c.Int(),
                        H4 = c.Int(),
                        H5 = c.Int(),
                        H6 = c.Int(),
                        H7 = c.Int(),
                        H8 = c.Int(),
                        H9 = c.Int(),
                        H10 = c.Int(),
                        IsmDeviceId = c.Int(nullable: false),
                        DeviceId = c.String(),
                        BlobUriImg = c.String(),
                        BlobUriColoredImg = c.String(),
                    })
                .PrimaryKey(t => t.FilamentDataId)
                .ForeignKey("dbo.IsmDevices", t => t.IsmDeviceId, cascadeDelete: true)
                .Index(t => t.IsmDeviceId);
            
            CreateTable(
                "dbo.ImagingProcessorWorkerInstances",
                c => new
                    {
                        RoleInstanceId = c.String(nullable: false, maxLength: 128),
                        McrInstalled = c.Boolean(nullable: false),
                        Timestamp = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.RoleInstanceId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.FilamentDatas", "IsmDeviceId", "dbo.IsmDevices");
            DropForeignKey("dbo.IsmDevices", "SoftwareId", "dbo.Softwares");
            DropForeignKey("dbo.IsmDevices", "LocationId", "dbo.Locations");
            DropForeignKey("dbo.Locations", "ContactId", "dbo.Contacts");
            DropForeignKey("dbo.IsmDevices", "HardwareId", "dbo.Hardwares");
            DropForeignKey("dbo.Commands", "IsmDeviceId", "dbo.IsmDevices");
            DropIndex("dbo.FilamentDatas", new[] { "IsmDeviceId" });
            DropIndex("dbo.Locations", new[] { "ContactId" });
            DropIndex("dbo.IsmDevices", new[] { "HardwareId" });
            DropIndex("dbo.IsmDevices", new[] { "SoftwareId" });
            DropIndex("dbo.IsmDevices", new[] { "LocationId" });
            DropIndex("dbo.Commands", new[] { "IsmDeviceId" });
            DropTable("dbo.ImagingProcessorWorkerInstances");
            DropTable("dbo.FilamentDatas");
            DropTable("dbo.Softwares");
            DropTable("dbo.Contacts");
            DropTable("dbo.Locations");
            DropTable("dbo.Hardwares");
            DropTable("dbo.IsmDevices");
            DropTable("dbo.Commands");
        }
    }
}
