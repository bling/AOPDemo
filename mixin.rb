module HelloWorld2
	def hello_world
		puts 'Hello World!'
	end
end

module HelloWorld
	def hello_world_2
		puts 'Hello World 2!'
	end
end

class A
	include HelloWorld
	include HelloWorld2
	def method_a
		puts 'method_a'
	end
end

b = A.new
b.method_a
b.hello_world
b.hello_world_2


