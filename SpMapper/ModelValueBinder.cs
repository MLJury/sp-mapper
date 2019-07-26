using System;
using System.Linq;

namespace Kama.DatabaseModel
{
    public interface IModelValueBinder
    {
        void SetValue(object model, string memberName, object value);
    }

    public class DefaultModelValueBinder: IModelValueBinder
    {
        public DefaultModelValueBinder(Type modelType)
        {
            _modelType = modelType;
            _properties = modelType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            _fields = modelType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        }

        readonly Type _modelType;
        readonly System.Reflection.FieldInfo[] _fields;
        readonly System.Reflection.PropertyInfo[] _properties;

        public void SetValue(object model, string memberName, object value)
        {
            var modelPropertiesName = string.Empty;
            var field = _fields.FirstOrDefault(f => f.Name.Equals(memberName));
            try
            {
                if (field != null)
                {
                    modelPropertiesName = field.Name;
                    field.SetValue(model, value);
                }
                else
                {
                    var prop = _properties.FirstOrDefault(p => p.Name.Equals(memberName));
                    if (prop != null)
                    {
                        modelPropertiesName = prop.Name;
                        prop.SetValue(model, value);
                    }
                }
            }
            catch (Exception e)
            {
                throw new ModelBinderException(e, modelPropertiesName, memberName);
            }
        }
    }
}
