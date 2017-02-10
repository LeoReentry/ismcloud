namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Addreferencetosoftwareversioninrelease : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Releases", "SoftwareVersionId", c => c.Int(nullable: false));
            CreateIndex("dbo.Releases", "SoftwareVersionId");
            AddForeignKey("dbo.Releases", "SoftwareVersionId", "dbo.SoftwareVersions", "SoftwareVersionId", cascadeDelete: true);
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Releases", "SoftwareVersionId", "dbo.SoftwareVersions");
            DropIndex("dbo.Releases", new[] { "SoftwareVersionId" });
            DropColumn("dbo.Releases", "SoftwareVersionId");
        }
    }
}
