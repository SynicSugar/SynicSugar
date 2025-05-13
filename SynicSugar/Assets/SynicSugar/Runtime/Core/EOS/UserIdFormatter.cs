using MemoryPack;

namespace SynicSugar {
    internal class UserIdFormatter : MemoryPackFormatter<UserId>
    {
        /// <summary>
        /// Serialize UserId as string.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        public override void Serialize(ref MemoryPackWriter writer, ref UserId value)
        {
            if (value == null || !value.IsValid()){
                writer.WriteNullObjectHeader();
                return;
            }

            writer.WriteString(value.ToString());
        }

        /// <summary>
        /// Deserialize UserId from cache by string. <br />
        /// If the corresponding value is not in cache, null is returned, so it must be generated and registered to UserIds in advance.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="value"></param>
        public override void Deserialize(ref MemoryPackReader reader, ref UserId value)
        {
            if (reader.PeekIsNull())
            {
                value = null;
                return;
            }
            
            value = UserId.GetUserId(reader.ReadString());
        }
    }
}