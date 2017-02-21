namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class NewUpdateLogsModel : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.UpdateLogs",
                c => new
                    {
                        UpdateLogId = c.Int(nullable: false, identity: true),
                        IsmDeviceId = c.Int(nullable: false),
                        ReleaseId = c.Int(nullable: false),
                        LogData = c.String(),
                        Date = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.UpdateLogId)
                .ForeignKey("dbo.IsmDevices", t => t.IsmDeviceId, cascadeDelete: true)
                .ForeignKey("dbo.Releases", t => t.ReleaseId, cascadeDelete: true)
                .Index(t => t.IsmDeviceId)
                .Index(t => t.ReleaseId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.UpdateLogs", "ReleaseId", "dbo.Releases");
            DropForeignKey("dbo.UpdateLogs", "IsmDeviceId", "dbo.IsmDevices");
            DropIndex("dbo.UpdateLogs", new[] { "ReleaseId" });
            DropIndex("dbo.UpdateLogs", new[] { "IsmDeviceId" });
            DropTable("dbo.UpdateLogs");
        }
    }
}
