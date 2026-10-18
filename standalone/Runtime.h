#pragma once
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#include <cstring>
#include <stdexcept>
#include <string>
#include <vector>

// Runtime metadata lookup. No game-version table or generated game offsets.
struct Runtime {
 HMODULE module{};
 template<class T>T api(const char* n) const {auto p=GetProcAddress(module,n);if(!p)throw std::runtime_error(std::string("Missing runtime API: ")+n);return reinterpret_cast<T>(p);}
 void* type(const char* ns,const char* name) const {
  auto domain=api<void*(*)()>("il2cpp_domain_get")();size_t count{};
  auto all=api<void**(*)(void*,size_t*)>("il2cpp_domain_get_assemblies")(domain,&count);void* found{};
  for(size_t i=0;i<count;i++){auto image=api<void*(*)(void*)>("il2cpp_assembly_get_image")(all[i]);
   auto c=api<void*(*)(void*,const char*,const char*)>("il2cpp_class_from_name")(image,ns,name);
   if(c){if(found&&found!=c)throw std::runtime_error(std::string("Ambiguous runtime type: ")+name);found=c;}}
  if(!found)throw std::runtime_error(std::string("Missing game type: ")+name);return found;
 }
 std::string typeName(const void* t) const {char* name=api<char*(*)(const void*)>("il2cpp_type_get_name")(t);std::string s=name?name:"";if(name)api<void(*)(void*)>("il2cpp_free")(name);return s;}
 void* method(void* c,const char* name,std::initializer_list<const char*> parameters,const char* returns) const {
  void* result{};for(auto owner=c;owner;owner=api<void*(*)(void*)>("il2cpp_class_get_parent")(owner)) {
   void* iterator{};while(auto m=api<void*(*)(void*,void**)>("il2cpp_class_get_methods")(owner,&iterator)) {
    if(std::strcmp(api<const char*(*)(void*)>("il2cpp_method_get_name")(m),name)||api<uint32_t(*)(void*)>("il2cpp_method_get_param_count")(m)!=parameters.size())continue;
    if(typeName(api<const void*(*)(void*)>("il2cpp_method_get_return_type")(m))!=returns)continue;
    bool match=true;uint32_t i=0;for(auto p:parameters)if(typeName(api<const void*(*)(void*,uint32_t)>("il2cpp_method_get_param")(m,i++))!=p)match=false;
    if(match){if(result)throw std::runtime_error(std::string("Ambiguous game method: ")+name);result=m;}}
   if(result)return result;
  }throw std::runtime_error(std::string("Missing game method contract: ")+name);
 }
 void* pointer(void* method) const {
  // MethodInfo.methodPointer is the first member in the game's current IL2CPP declarations.
  auto p=*static_cast<void**>(method);MEMORY_BASIC_INFORMATION m{};
  if(!p||!VirtualQuery(p,&m,sizeof(m))||m.State!=MEM_COMMIT||!(m.Protect&(PAGE_EXECUTE|PAGE_EXECUTE_READ|PAGE_EXECUTE_READWRITE|PAGE_EXECUTE_WRITECOPY)))throw std::runtime_error("The game method is not executable.");return p;
 }
 void* field(void* c,const char* name) const {for(auto owner=c;owner;owner=api<void*(*)(void*)>("il2cpp_class_get_parent")(owner)){auto f=api<void*(*)(void*,const char*)>("il2cpp_class_get_field_from_name")(owner,name);if(f)return f;}throw std::runtime_error(std::string("Missing game field: ")+name);}
 void* fieldClass(void* c,const char* name) const {return api<void*(*)(const void*)>("il2cpp_class_from_type")(api<const void*(*)(void*)>("il2cpp_field_get_type")(field(c,name)));}
 int offset(void* c,const char* name,int expected) const {
  auto f=field(c,name);auto t=api<const void*(*)(void*)>("il2cpp_field_get_type")(f);
  auto flags=api<uint32_t(*)(void*)>("il2cpp_field_get_flags")(f);auto code=api<int(*)(const void*)>("il2cpp_type_get_type")(t);
  auto fc=api<void*(*)(const void*)>("il2cpp_class_from_type")(t);
  if(api<bool(*)(void*)>("il2cpp_class_is_enum")(fc))code=api<int(*)(const void*)>("il2cpp_type_get_type")(api<const void*(*)(void*)>("il2cpp_class_enum_basetype")(fc));
  bool match=expected==0?!api<bool(*)(void*)>("il2cpp_class_is_valuetype")(fc):code==expected;
  int value=api<int(*)(void*)>("il2cpp_field_get_offset")(f);
  if((flags&0x10)||!match||value<0||value>65536)throw std::runtime_error(std::string("Changed game field contract: ")+name);return value;
 }
 int enumMember(void* c,const char* member) const {int v{};api<void(*)(void*,void*)>("il2cpp_field_static_get_value")(field(c,member),&v);return v;}
};
template<class T>T member(void* o,int offset) {return *reinterpret_cast<T*>(static_cast<char*>(o)+offset);}
