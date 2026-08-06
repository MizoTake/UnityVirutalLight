namespace MizoTake.VirtualLight
{
    public interface IVirtualLightSystem
    {
        VirtualLightHandle Register(in VirtualLightDescriptor descriptor);
        void Update(VirtualLightHandle handle, in VirtualLightDescriptor descriptor);
        void Unregister(VirtualLightHandle handle);
        void ClearGeneratedLights();
        void SetQuality(VirtualLightQuality quality);
    }
}
