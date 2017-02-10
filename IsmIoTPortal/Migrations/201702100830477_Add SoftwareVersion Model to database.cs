namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddSoftwareVersionModeltodatabase : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.SoftwareVersions",
                c => new
                    {
                        SoftwareVersionId = c.Int(nullable: false, identity: true),
                        Prefix = c.String(),
                        Suffix = c.String(),
                    })
                .PrimaryKey(t => t.SoftwareVersionId);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.SoftwareVersions");
        }
    }
}
