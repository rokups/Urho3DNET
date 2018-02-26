//
// Copyright (c) 2008-2018 the Urho3D project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

#include <Urho3D/IO/Log.h>
#include <cppast/cpp_function.hpp>
#include <cppast/cpp_member_function.hpp>
#include <Declarations/Function.hpp>
#include <Declarations/Variable.hpp>
#include "UnknownTypesPass.h"


namespace Urho3D
{

void UnknownTypesPass::Start()
{
    generator_ = GetSubsystem<GeneratorContext>();
}

bool UnknownTypesPass::Visit(Declaration* decl, Event event)
{
    if (decl->source_ == nullptr)
        return true;

    auto checkFunctionParams = [&](const std::vector<const cppast::cpp_function_parameter*>& params) {
        for (const auto& param : params)
        {
            if (!generator_->IsAcceptableType(param->type()))
            {
                URHO3D_LOGINFOF("Ignore: %s, unknown parameter type %s", decl->symbolName_.c_str(),
                                cppast::to_string(param->type()).c_str());
                return false;
            }
        }
        return true;
    };

    auto checkFunctionTypes = [&](const cppast::cpp_type& returnType,
                                  const std::vector<const cppast::cpp_function_parameter*>& params) {
        if (!generator_->IsAcceptableType(returnType))
        {
            URHO3D_LOGINFOF("Ignore: %s, unknown return type %s", decl->symbolName_.c_str(),
                cppast::to_string(returnType).c_str());
            return false;
        }
        return checkFunctionParams(params);
    };

    if (decl->IsFunctionLike())
    {
        Function* func = static_cast<Function*>(decl);
        if (decl->name_.find("operator") == 0)
        {
            decl->Ignore();
            URHO3D_LOGINFOF("Ignore: %s, operators not supported.", decl->name_.c_str());
        }
        else if (!checkFunctionTypes(*func->returnType_, func->parameters_))
            decl->Ignore();
    }
    else if (decl->kind_ == Declaration::Kind::Variable)
    {
        Variable* var = static_cast<Variable*>(decl);
        const auto& type = var->GetType();
        if (!generator_->IsAcceptableType(type))
        {
            decl->Ignore();
            URHO3D_LOGINFOF("Ignore: %s, unknown return type %s", decl->symbolName_.c_str(),
                            cppast::to_string(type).c_str());
        }
    }
    else if (decl->kind_ == Declaration::Kind::Class)
    {
        if (!generator_->symbols_.Has(decl->symbolName_))
            decl->Ignore();
    }
    return true;
}
}
