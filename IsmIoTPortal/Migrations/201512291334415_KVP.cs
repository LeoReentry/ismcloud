namespace IsmIoTPortal.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class KVP : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.FilamentDatas", "P1_Key", c => c.String());
            AddColumn("dbo.FilamentDatas", "P1_Value", c => c.Int(nullable: false));
            AddColumn("dbo.FilamentDatas", "P2_Key", c => c.String());
            AddColumn("dbo.FilamentDatas", "P2_Value", c => c.Int(nullable: false));
            AddColumn("dbo.FilamentDatas", "P3_Key", c => c.String());
            AddColumn("dbo.FilamentDatas", "P3_Value", c => c.Int(nullable: false));
            AddColumn("dbo.FilamentDatas", "P4_Key", c => c.String());
            AddColumn("dbo.FilamentDatas", "P4_Value", c => c.Int(nullable: false));
            AddColumn("dbo.FilamentDatas", "P5_Key", c => c.String());
            AddColumn("dbo.FilamentDatas", "P5_Value", c => c.Int(nullable: false));
            AddColumn("dbo.FilamentDatas", "P6_Key", c => c.String());
            AddColumn("dbo.FilamentDatas", "P6_Value", c => c.Int(nullable: false));
            AddColumn("dbo.FilamentDatas", "P7_Key", c => c.String());
            AddColumn("dbo.FilamentDatas", "P7_Value", c => c.Int(nullable: false));
            AddColumn("dbo.FilamentDatas", "P8_Key", c => c.String());
            AddColumn("dbo.FilamentDatas", "P8_Value", c => c.Int(nullable: false));
            AddColumn("dbo.FilamentDatas", "P9_Key", c => c.String());
            AddColumn("dbo.FilamentDatas", "P9_Value", c => c.Int(nullable: false));
            AddColumn("dbo.FilamentDatas", "P10_Key", c => c.String());
            AddColumn("dbo.FilamentDatas", "P10_Value", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.FilamentDatas", "P10_Value");
            DropColumn("dbo.FilamentDatas", "P10_Key");
            DropColumn("dbo.FilamentDatas", "P9_Value");
            DropColumn("dbo.FilamentDatas", "P9_Key");
            DropColumn("dbo.FilamentDatas", "P8_Value");
            DropColumn("dbo.FilamentDatas", "P8_Key");
            DropColumn("dbo.FilamentDatas", "P7_Value");
            DropColumn("dbo.FilamentDatas", "P7_Key");
            DropColumn("dbo.FilamentDatas", "P6_Value");
            DropColumn("dbo.FilamentDatas", "P6_Key");
            DropColumn("dbo.FilamentDatas", "P5_Value");
            DropColumn("dbo.FilamentDatas", "P5_Key");
            DropColumn("dbo.FilamentDatas", "P4_Value");
            DropColumn("dbo.FilamentDatas", "P4_Key");
            DropColumn("dbo.FilamentDatas", "P3_Value");
            DropColumn("dbo.FilamentDatas", "P3_Key");
            DropColumn("dbo.FilamentDatas", "P2_Value");
            DropColumn("dbo.FilamentDatas", "P2_Key");
            DropColumn("dbo.FilamentDatas", "P1_Value");
            DropColumn("dbo.FilamentDatas", "P1_Key");
        }
    }
}
