namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class test : DbMigration
    {
        public override void Up()
        {
            DropTable("dbo.SoftwareVersions");
        }
        
        public override void Down()
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
    }
}
