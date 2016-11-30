namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class UpdateNewDevices : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.NewDevices", "Approved", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.NewDevices", "Approved");
        }
    }
}
