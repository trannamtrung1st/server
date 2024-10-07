namespace AasxServerStandardBib.Models
{
    public class GetSimpleUomDto
    {
        public int? Id { get; set; }
        public string Name { get; set; }
        public string Abbreviation { get; set; }

        public GetSimpleUomDto()
        {
        }

        public GetSimpleUomDto(int? id, string name, string abbreviation)
        {
            Id = id;
            Name = name;
            Abbreviation = abbreviation;
        }
    }
}
