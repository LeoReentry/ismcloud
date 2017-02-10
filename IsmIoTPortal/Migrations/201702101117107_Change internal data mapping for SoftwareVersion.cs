namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class ChangeinternaldatamappingforSoftwareVersion : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.SoftwareVersions", "InternalReleaseNum", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.SoftwareVersions", "InternalReleaseNum");
        }
    }
}
